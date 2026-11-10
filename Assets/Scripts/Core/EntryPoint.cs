using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Mophia.Data;

namespace Mophia.Core
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private ChartLoader  chartLoader;
        [SerializeField] private NotesRunner  notesRunner;
        [SerializeField] private VideoPlayer  videoPlayer;
        [SerializeField] private UnityEngine.UI.RawImage videoDisplay; // Canvas背面のRawImage
        [SerializeField] private Camera       gameCamera;

        private void Start()
        {
            ChartData chart = chartLoader.Load();
            if (chart == null) return;

            ApplyBackgroundColor(chart);
            notesRunner.Initialize(chart);
            StartCoroutine(PrepareAndStart(chart));
        }

        private void ApplyBackgroundColor(ChartData chart)
        {
            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null) return;

            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = chart.meta != null ? chart.meta.BackgroundColor : Color.white;
        }

        private IEnumerator PrepareAndStart(ChartData chart)
        {
            // mp4パスを選択中の曲フォルダから構築(背景表示にはbackgroundMp4があればそちらを使う。
            // 曲選択画面のプレビューは常に元のmp4を使うので、ここでの切り替えはGamePlayのみに影響する)
            string mp4Path = System.IO.Path.Combine(
                SongSelection.GetSongDirectory(), chart.meta.BackgroundMp4OrDefault);

            videoPlayer.url            = "file://" + mp4Path;
            videoPlayer.playOnAwake    = false;
            videoPlayer.isLooping      = false;
            videoPlayer.renderMode     = VideoRenderMode.RenderTexture;

            // 動画をプリペア（バッファリング）
            videoPlayer.Prepare();
            yield return new WaitUntil(() => videoPlayer.isPrepared);

            // RenderTextureはPrepare完了後、実サイズが分かってから1回だけ作る。
            // (仮サイズで先に作ってPlay()直前に作り直すと、GPU確保がPlay()と重なって
            // 音声バッファが詰まりやすくなるため)
            var rt = new RenderTexture((int)videoPlayer.width, (int)videoPlayer.height, 0);
            videoPlayer.targetTexture  = rt;
            if (videoDisplay != null) videoDisplay.texture = rt;

            Debug.Log($"[EntryPoint] VideoPlayer準備完了: {chart.meta.mp4} ({videoPlayer.width}x{videoPlayer.height})");

            // RenderTexture確保の完了を1フレーム分待ってからPlay()する
            yield return null;

            // VideoPlayerをNotesRunnerに渡してからゲーム開始
            notesRunner.SetVideoPlayer(videoPlayer);
            videoPlayer.Play();
            notesRunner.StartGame();
        }
    }
}
