using System.Collections;
using Mophia.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mophia.UI
{
    /// <summary>
    /// 動画入力画面の起動処理: アップロード用HTTPサーバとcloudflaredトンネルを開始し、
    /// 発行された公開URLをQRコード化して表示する。トンネルの確立には数秒かかるため非同期に待つ。
    /// スマホから受信した動画はそのままSongConverterへ渡して変換を開始する。
    /// </summary>
    public class VideoInputScreen : MonoBehaviour
    {
        [SerializeField] private VideoUploadServer uploadServer;
        [SerializeField] private SongConverter     songConverter;
        [SerializeField] private RawImage          qrImage;
        [SerializeField] private TMP_Text          urlText;

        [SerializeField] private float tunnelTimeoutSeconds = 20f;

        private void Start()
        {
            uploadServer.OnVideoReceived += OnVideoReceived;
            uploadServer.StartServerAndTunnel();
            StartCoroutine(WaitForUrlRoutine());
        }

        // 「再接続」ボタンから呼ぶ。PCのネットワーク接続先が変わってcloudflaredのトンネルが
        // 復帰しなくなった場合に、サーバ/トンネルを作り直して新しいQRを発行する
        public void Reconnect()
        {
            StopAllCoroutines();
            uploadServer.Restart();
            StartCoroutine(WaitForUrlRoutine());
        }

        private IEnumerator WaitForUrlRoutine()
        {
            urlText.text = "スマホアップロード用のURLを準備中...";

            float elapsed = 0f;
            while (uploadServer.PublicUrl == null && !uploadServer.TunnelFailed && elapsed < tunnelTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (uploadServer.PublicUrl != null)
            {
                string url = uploadServer.PublicUrl;
                urlText.text = url;

                Texture2D qrTexture = QrCodeGenerator.GenerateTexture(url);
                if (qrTexture != null) qrImage.texture = qrTexture;
            }
            else
            {
                urlText.text = "スマホアップロードを利用できません(cloudflared.exe未配置、またはタイムアウトしました)";
            }
        }

        private void OnVideoReceived(string path, string title)
        {
            songConverter.ConvertFile(path, title);
        }
    }
}
