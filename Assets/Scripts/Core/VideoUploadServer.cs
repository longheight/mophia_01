using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Mophia.Core
{
    /// <summary>
    /// スマホから動画をアップロードできるようにする、最小限のHTTPサーバ。
    /// HttpListenerは非管理者権限だとURL ACLの登録(netsh)が必要になるため、
    /// 代わりにTcpListenerで最小限のHTTP/1.1を自前パースしている(認証なし)。
    /// GET  /         → アップロード用の簡易ページ(スマホのブラウザで開く)
    /// POST /upload?name=xxx.mp4 → リクエストボディをそのままファイルとして保存
    ///
    /// PCが同一LANのWi-Fiを持たない(有線オンリー)環境でも使えるよう、
    /// cloudflared(Cloudflare Tunnel)でこのローカルサーバをインターネットに公開する。
    /// アカウント登録不要の"quick tunnel"機能を使うため、追加設定は不要。
    /// </summary>
    public class VideoUploadServer : MonoBehaviour
    {
        [SerializeField] private int port = 7890;

        [Tooltip("StreamingAssets相対のcloudflared.exeのパス。ビルドに同梱されるので、実行するアプリの利用者は何も用意しなくてよい")]
        [SerializeField] private string cloudflaredRelativePath = "Tools/cloudflared.exe";

        /// <summary>動画を受信した(保存先パス, 入力されたタイトル(未入力ならnull))</summary>
        public event Action<string, string> OnVideoReceived;

        /// <summary>cloudflaredが発行した公開URL。準備できるまではnull。</summary>
        public string PublicUrl => _publicUrl;
        public bool TunnelFailed => _tunnelFailed;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;

        private Win32Process.Handle _tunnelHandle;
        private volatile string _publicUrl;
        private volatile bool _tunnelFailed;

        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private readonly object _queueLock = new object();

        // Application.temporaryCachePathはUnity APIなのでメインスレッドでしか呼べない。
        // リクエスト処理はバックグラウンドスレッドで行うため、事前にメインスレッドでキャッシュしておく。
        private string _tempCachePath;

        private void Awake()
        {
            _tempCachePath = Application.temporaryCachePath;
        }

        /// <summary>ローカルサーバを起動し、cloudflaredでインターネットに公開する。公開URLはPublicUrlで非同期に取得できる。</summary>
        public void StartServerAndTunnel()
        {
            if (_running) return;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _running = true;

            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();

            StartTunnel();
        }

        public string GetLocalUrl() => $"http://{GetLocalIPAddress()}:{port}/";

        private void StartTunnel()
        {
            string cloudflaredPath = Path.Combine(Application.streamingAssetsPath, cloudflaredRelativePath);
            if (!File.Exists(cloudflaredPath))
            {
                Debug.LogError($"[VideoUploadServer] cloudflared.exeが見つかりません: {cloudflaredPath}");
                _tunnelFailed = true;
                return;
            }

            // --protocol http2: QUIC(UDP)がネットワーク側でブロックされていると接続できないことがあるため、
            // TCPベースのHTTP/2を明示して繋がりやすくする
            string commandLine = $"\"{cloudflaredPath}\" tunnel --protocol http2 --url http://localhost:{port}";
            string workingDir  = Path.GetDirectoryName(cloudflaredPath);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var handle = Win32Process.StartBackground(commandLine, workingDir, line =>
                {
                    if (string.IsNullOrEmpty(line)) return;
                    Debug.Log($"[cloudflared] {line}");
                    Match m = Regex.Match(line, @"https://\S+\.trycloudflare\.com");
                    if (m.Success) _publicUrl = m.Value;
                });

                if (handle == null)
                {
                    Debug.LogError("[VideoUploadServer] cloudflaredの起動に失敗しました");
                    _tunnelFailed = true;
                }
                else
                {
                    _tunnelHandle = handle;
                }
            });
        }

        /// <summary>
        /// cloudflaredのトンネルだけを作り直す。ローカルのTcpListenerはネットワーク経路に依存せず
        /// 壊れないので触らない(同じポートに即座に再バインドしようとするとSocketExceptionになるため)。
        /// PCのネットワーク接続先が変わるとcloudflaredの接続が復帰しないことがあるため、
        /// その場合の手動リカバリ手段として使う。
        /// </summary>
        public void Restart()
        {
            _tunnelHandle?.Kill();
            _tunnelHandle = null;
            _publicUrl    = null;
            _tunnelFailed = false;
            StartTunnel();
        }

        private void OnDestroy()
        {
            _running = false;
            try { _listener?.Stop(); } catch { /* シーン終了時の後始末なので握りつぶす */ }
            _tunnelHandle?.Kill();
        }

        private void Update()
        {
            lock (_queueLock)
            {
                while (_mainThreadActions.Count > 0)
                    _mainThreadActions.Dequeue().Invoke();
            }
        }

        private void EnqueueMainThread(Action action)
        {
            lock (_queueLock) { _mainThreadActions.Enqueue(action); }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; } // Stop()でリスナーが閉じられた

                ThreadPool.QueueUserWorkItem(_ => HandleClientSafe(client));
            }
        }

        private void HandleClientSafe(object clientObj)
        {
            var client = (TcpClient)clientObj;
            try { HandleClient(client); }
            catch (Exception e) { Debug.LogWarning($"[VideoUploadServer] 通信エラー: {e.Message}"); }
            finally { client.Close(); }
        }

        private void HandleClient(TcpClient client)
        {
            using NetworkStream stream = client.GetStream();

            string requestLine = ReadLine(stream);
            if (string.IsNullOrEmpty(requestLine)) return;

            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2) return;
            string method = parts[0];
            string path   = parts[1];

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string line;
            while (!string.IsNullOrEmpty(line = ReadLine(stream)))
            {
                int idx = line.IndexOf(':');
                if (idx > 0) headers[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
            }

            // cloudflaredがPOSTを中継する際「Expect: 100-continue」を付けてくることがあり、
            // ここで先にContinueを返さないと相手がボディ送信を待ち続けてタイムアウト(502)になる
            if (headers.TryGetValue("Expect", out string expectValue) &&
                expectValue.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                byte[] continueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                stream.Write(continueBytes, 0, continueBytes.Length);
                stream.Flush();
            }

            if (method == "GET" && (path == "/" || path.StartsWith("/?")))
            {
                WriteHttpResponse(stream, "200 OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(UploadPageHtml));
                return;
            }

            if (method == "POST" && path.StartsWith("/upload"))
            {
                long contentLength = 0;
                if (headers.TryGetValue("Content-Length", out string clStr)) long.TryParse(clStr, out contentLength);

                string rawName = ExtractQueryParam(path, "name") ?? "video.mp4";
                string safeName = SanitizeFileName(rawName);
                string title    = SanitizeTitle(ExtractQueryParam(path, "title"));

                byte[] data = ReadExact(stream, contentLength);
                string savePath = Path.Combine(_tempCachePath, $"upload_{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}");
                File.WriteAllBytes(savePath, data);

                // 変換が今すぐ始まるか順番待ちになるかはUnity側の状態次第でここでは判定できないため、
                // 両方に当てはまる表現にしておく
                WriteHttpResponse(stream, "200 OK", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("送信完了。変換の順番待ちに追加しました。"));
                EnqueueMainThread(() => OnVideoReceived?.Invoke(savePath, title));
                return;
            }

            WriteHttpResponse(stream, "404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"));
        }

        private static string ReadLine(NetworkStream stream)
        {
            var sb = new StringBuilder();
            int prev = -1;
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                if (prev == '\r' && b == '\n')
                {
                    if (sb.Length > 0) sb.Length -= 1; // 末尾の\rを除去
                    return sb.ToString();
                }
                sb.Append((char)b);
                prev = b;
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static byte[] ReadExact(NetworkStream stream, long length)
        {
            var buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(buffer, offset, (int)(length - offset));
                if (read <= 0) break;
                offset += read;
            }
            return buffer;
        }

        private static void WriteHttpResponse(NetworkStream stream, string status, string contentType, byte[] body)
        {
            string header = $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string ExtractQueryParam(string path, string key)
        {
            int qIdx = path.IndexOf('?');
            if (qIdx < 0) return null;
            string query = path.Substring(qIdx + 1);
            foreach (string kv in query.Split('&'))
            {
                int eIdx = kv.IndexOf('=');
                if (eIdx < 0) continue;
                if (kv.Substring(0, eIdx) == key)
                    return Uri.UnescapeDataString(kv.Substring(eIdx + 1));
            }
            return null;
        }

        // パストラバーサル対策: ディレクトリ区切りを含む部分は捨て、ファイル名だけを残す
        private static string SanitizeFileName(string name)
        {
            string fileName = Path.GetFileName(name);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "video.mp4";
            return fileName;
        }

        // Pythonへコマンドライン引数としてそのまま渡すため、引用符を除去し長さを制限しておく
        private static string SanitizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            title = title.Replace("\"", "").Trim();
            if (title.Length > 60) title = title.Substring(0, 60);
            return string.IsNullOrEmpty(title) ? null : title;
        }

        private static string GetLocalIPAddress()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        return addr.Address.ToString();
                }
            }
            return "127.0.0.1";
        }

        private const string UploadPageHtml = @"<!DOCTYPE html>
<html lang=""ja""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Mophia 動画アップロード</title>
<style>
body{font-family:sans-serif;text-align:center;padding:40px 20px;background:#fff;color:#000;}
h1{font-size:20px;}
#status{margin-top:20px;font-size:16px;white-space:pre-wrap;}
label{display:block;margin-top:20px;font-size:14px;text-align:left;max-width:320px;margin-left:auto;margin-right:auto;}
input[type=text]{width:100%;box-sizing:border-box;padding:10px;font-size:16px;border:2px solid #000;margin-top:6px;}
input[type=file]{margin-top:20px;}
button{margin-top:20px;padding:12px 32px;font-size:16px;border:2px solid #000;background:#fff;}
</style></head>
<body>
<h1>動画をアップロード</h1>
<label for=""title"">タイトル(任意)
<input type=""text"" id=""title"" placeholder=""未入力ならファイル名を使います"" maxlength=""60"">
</label>
<br><input type=""file"" id=""file"" accept=""video/*"">
<div style=""font-size:12px;color:#666;margin-top:6px;"">動画は5秒以内にしてください</div>
<br><button id=""send"">送信</button>
<div id=""status""></div>
<script>
var MAX_DURATION_SEC = 5;

function readDuration(file) {
  return new Promise(function (resolve, reject) {
    var video = document.createElement('video');
    video.preload = 'metadata';
    video.onloadedmetadata = function () {
      URL.revokeObjectURL(video.src);
      resolve(video.duration);
    };
    video.onerror = function () {
      URL.revokeObjectURL(video.src);
      reject('動画を読み込めませんでした');
    };
    video.src = URL.createObjectURL(file);
  });
}

document.getElementById('send').onclick = function () {
  var f = document.getElementById('file').files[0];
  if (!f) { document.getElementById('status').textContent = 'ファイルを選択してください'; return; }

  document.getElementById('status').textContent = '動画を確認中...';
  readDuration(f).then(function (duration) {
    if (duration > MAX_DURATION_SEC) {
      document.getElementById('status').textContent =
        '動画は' + MAX_DURATION_SEC + '秒以内にしてください(この動画は' + duration.toFixed(1) + '秒です)';
      return;
    }

    var title = document.getElementById('title').value;
    document.getElementById('status').textContent = 'アップロード中...';
    var url = '/upload?name=' + encodeURIComponent(f.name) + '&title=' + encodeURIComponent(title);
    fetch(url, { method: 'POST', body: f })
      .then(function (r) { return r.text().then(function (t) { return r.ok ? t : Promise.reject(t); }); })
      .then(function (t) { document.getElementById('status').textContent = t; })
      .catch(function (e) { document.getElementById('status').textContent = '送信に失敗しました: ' + e; });
  }).catch(function (e) {
    document.getElementById('status').textContent = e;
  });
};
</script>
</body></html>";
    }
}
