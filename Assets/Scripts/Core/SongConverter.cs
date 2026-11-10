using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Mophia.Core
{
    /// <summary>
    /// mp4を選んで、ddc_demo.py → sam2_demo_2.py を順に実行し、
    /// StreamingAssets/Songs/&lt;曲名&gt;/ に譜面一式を生成する。
    /// 旧main.cpp(別アプリ)がやっていた変換オーケストレーションをUnity側に統合したもの。
    ///
    /// System.Diagnostics.Process + WorkingDirectory の組み合わせだと、Unity(呼び出し元)
    /// 自身のカレントディレクトリまで書き換わってしまう(Editorがこれをフェイタルエラーとして
    /// 検知する)ため、Win32のCreateProcessを直接呼んで子プロセス側だけに作業ディレクトリを
    /// 与えるようにしている。
    /// </summary>
    public class SongConverter : MonoBehaviour
    {
        [Header("開発機のPython環境(この機体固定のパス)")]
        [SerializeField] private string pythonExe        = "python";
        [SerializeField] private string pythonScriptsDir = @"C:\tachi\code\mophia_dmeo_1\x64\Debug";
        [SerializeField] private float  onsetThreshold    = 0.2f;

        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image    progressBarFill;
        [SerializeField] private TMP_Text progressPercentText;
        [SerializeField] private Button convertButton;
        [SerializeField] private SongSelectManager songSelectManager;

        [Tooltip("songSelectManagerが未設定(=SongSelect画面の外から呼ばれた)場合、完了後にこのシーンへ遷移する")]
        [SerializeField] private string songSelectSceneName = "SongSelect";

        // 各ステップのprint("PROGRESS <0-100>")を全体の0..1へ写像する重み。
        // ddc_demo(オンセット検出)は短時間、sam2_demo_2(SAM2追跡)が大半を占めるため9:1で配分。
        private const float Step1Weight = 0.1f;
        private const float Step2Weight = 0.9f;

        // ワーカースレッド(プロセスのstdout読み取り)から書き込まれ、コルーチンが毎フレーム読む。
        private volatile float _progress01;

        // 変換中に別の動画が来た場合はキューに積み、今の変換が終わってから順番に処理する
        // (同時にPythonプロセスを2つ走らせるとCUDA/進捗表示が競合して壊れるため)
        private readonly Queue<(string path, string title)> _pendingQueue = new Queue<(string, string)>();
        private bool _isConverting;

        public void PickAndConvert()
        {
            string mp4Path = NativeFileDialog.OpenFile("MP4 Files\0*.mp4\0\0", "変換するmp4を選択");
            if (string.IsNullOrEmpty(mp4Path)) return;

            EnqueueOrStart(mp4Path, null);
        }

        // QRコード経由のスマホアップロードなど、ファイルパスが既に分かっている場合の入口。
        // titleはアップロードページで入力された表示名(未指定ならファイル名を使う)
        public void ConvertFile(string videoPath, string title = null)
        {
            if (string.IsNullOrEmpty(videoPath)) return;
            EnqueueOrStart(videoPath, title);
        }

        private void EnqueueOrStart(string path, string title)
        {
            if (_isConverting)
            {
                _pendingQueue.Enqueue((path, title));
                SetStatus($"変換中... (待機中: {_pendingQueue.Count}件)");
                return;
            }
            StartCoroutine(ConvertRoutine(path, title));
        }

        private IEnumerator ConvertRoutine(string mp4Path, string title)
        {
            _isConverting = true;
            if (convertButton != null) convertButton.interactable = false;
            try
            {
                _progress01 = 0f;
                UpdateProgressUI();

                string songName  = Path.GetFileNameWithoutExtension(mp4Path);
                string outputDir = Path.Combine(SongSelection.SongsRootPath, songName);
                Directory.CreateDirectory(outputDir);

                string onsetsJsonPath = Path.Combine(
                    Path.GetDirectoryName(mp4Path) ?? "", songName + "_onsets.json");

                SetStatus("変換中... (1/2) オンセット検出");
                bool step1Ok = false, step1Done = false;
                RunProcessAsync(
                    $"\"{Combine("ddc_demo.py")}\" \"{mp4Path}\" {onsetThreshold:F2}",
                    0f, Step1Weight,
                    ok => { step1Ok = ok; step1Done = true; });
                while (!step1Done) { UpdateProgressUI(); yield return null; }

                if (!step1Ok)
                {
                    SetStatus("エラー: オンセット検出に失敗しました");
                    yield break;
                }

                string titleArg = string.IsNullOrEmpty(title) ? "" : $" --title \"{title.Replace("\"", "")}\"";

                SetStatus("変換中... (2/2) オブジェクト検出・追跡(時間がかかります)");
                bool step2Ok = false, step2Done = false;
                RunProcessAsync(
                    $"\"{Combine("sam2_demo_2.py")}\" \"{mp4Path}\" \"{onsetsJsonPath}\" \"{outputDir}\"{titleArg}",
                    Step1Weight, Step2Weight,
                    ok => { step2Ok = ok; step2Done = true; });
                while (!step2Done) { UpdateProgressUI(); yield return null; }

                if (!step2Ok)
                {
                    SetStatus("エラー: 変換に失敗しました");
                    yield break;
                }

                _progress01 = 1f;
                UpdateProgressUI();
                SetStatus($"完了: {(string.IsNullOrEmpty(title) ? songName : title)}");

                if (songSelectManager != null)
                    songSelectManager.Refresh();
                else if (!string.IsNullOrEmpty(songSelectSceneName))
                    SceneManager.LoadScene(songSelectSceneName);
            }
            finally
            {
                _isConverting = false;
                if (convertButton != null) convertButton.interactable = true;

                if (_pendingQueue.Count > 0)
                {
                    var (nextPath, nextTitle) = _pendingQueue.Dequeue();
                    StartCoroutine(ConvertRoutine(nextPath, nextTitle));
                }
            }
        }

        private string Combine(string scriptFileName) =>
            Path.Combine(pythonScriptsDir, scriptFileName);

        private void UpdateProgressUI()
        {
            if (progressBarFill != null) progressBarFill.fillAmount = _progress01;
            if (progressPercentText != null) progressPercentText.text = $"{Mathf.RoundToInt(_progress01 * 100)}%";
        }

        // stepOffset01/stepWeight01: このステップが吐く"PROGRESS 0-100"を全体の0..1のどの範囲に写像するか
        private void RunProcessAsync(string scriptArgs, float stepOffset01, float stepWeight01, Action<bool> onComplete)
        {
            // -u: stdoutをバッファリングしない(パイプ越しだとデフォルトでブロックバッファになり、
            //     PROGRESS行がプロセス終了までUnity側に届かなくなるため)。-X utf8: 日本語ログの文字化け防止。
            string commandLine = $"\"{pythonExe}\" -u -X utf8 {scriptArgs}";
            string workingDir  = pythonScriptsDir;

            // バックグラウンドスレッドで起動→出力読み取り→終了待ちを行う(コルーチンをブロックしないため)。
            // onOutputLine/onCompleteはこのスレッドから呼ばれるので、_progress01への単純代入以外はここで行わない。
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok;
                try
                {
                    ok = Win32Process.RunAndWait(commandLine, workingDir, line =>
                    {
                        if (line != null && line.StartsWith("PROGRESS "))
                        {
                            if (int.TryParse(line.Substring("PROGRESS ".Length).Trim(), out int pct))
                                _progress01 = stepOffset01 + Mathf.Clamp01(pct / 100f) * stepWeight01;
                        }
                        else if (!string.IsNullOrEmpty(line))
                        {
                            Debug.Log($"[python] {line}");
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SongConverter] プロセス起動失敗: {e.Message}");
                    ok = false;
                }
                onComplete?.Invoke(ok);
            });
        }

        private void SetStatus(string message)
        {
            Debug.Log($"[SongConverter] {message}");
            if (statusText != null) statusText.text = message;
        }
    }
}
