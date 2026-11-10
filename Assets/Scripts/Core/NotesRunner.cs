using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using Mophia.Data;
using Mophia.Input;
using Mophia.UI;

namespace Mophia.Core
{
    public class NotesRunner : MonoBehaviour
    {
        [SerializeField] private GameObject   notePrefab;
        [SerializeField] private Transform    noteParent;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private UIManager    uiManager;
        [SerializeField] private float        spawnLeadTime = 1.0f;

        [Tooltip("映像開始からノーツ生成開始までの遅延(フレーム数)。デフォルト90f≒1.5秒(60fps)")]
        [SerializeField] private int offsetFrames = 0;

        private ChartData   _chart;
        private bool        _isRunning;
        private VideoPlayer _videoPlayer;
        private float       _offsetSeconds;
        private float       _fallbackTime;

        // ゲームタイム: VideoPlayerの生の時刻（オフセットは引かない）
        private float RawTime => _videoPlayer != null && _videoPlayer.isPlaying
            ? (float)_videoPlayer.time
            : _fallbackTime;

        // ノーツ判定に使うゲームタイム: 映像時刻そのまま
        private float GameTime => RawTime;

        // オブジェクトプール
        private Queue<NoteView>  _pool;
        private List<NoteView>   _allViews; // 事前生成した全NoteView

        private Queue<(NoteData note, string preset, ColorData color)> _pendingNotes;

        public void Initialize(ChartData chart)
        {
            _chart = chart;

            float fps      = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60f;
            _offsetSeconds = offsetFrames / fps;

            // ノーツリストを構築
            var allNotes = new List<(NoteData, string, ColorData)>();
            foreach (var section in chart.sections)
            {
                foreach (var note in section.notes)
                {
                    string preset = section.presets?.Get(note.noteType) ?? "shrink_sharp";
                    allNotes.Add((note, preset, section.color));
                }
            }
            allNotes.Sort((a, b) => a.Item1.time.CompareTo(b.Item1.time));
            uiManager?.SetTotalNotes(allNotes.Count);

            _pendingNotes = new Queue<(NoteData, string, ColorData)>();
            foreach (var item in allNotes)
                _pendingNotes.Enqueue(item);

            // オブジェクトプール: 全ノーツ分を事前生成して非表示にしておく
            _pool     = new Queue<NoteView>();
            _allViews = new List<NoteView>();
            var spawned = new List<(GameObject obj, string preset)>(allNotes.Count);
            for (int i = 0; i < allNotes.Count; i++)
            {
                var (_, preset, _) = allNotes[i];
                GameObject obj  = Instantiate(notePrefab, noteParent);
                var        view = obj.GetComponent<NoteView>();
                _pool.Enqueue(view);
                _allViews.Add(view);
                spawned.Add((obj, preset));
            }

            // Instantiate直後に即SetActive(false)すると、AnimatorのControllerバインドが
            // 完了しきる前に中断されてしまい、ノーツによって初期化されたりされなかったりする
            // (再有効化してもRebind()だけでは直りきらないケースがある)。
            // 1フレーム待ってから、各ノーツが実際に使うpresetをWarmUpして
            // (Animator/Spriteの初回使用コストを音声再生が始まる前に前倒しして払っておく)
            // それから非表示にする。
            StartCoroutine(WarmUpAndDeactivateNextFrame(spawned));

            Debug.Log($"[NotesRunner] {_pendingNotes.Count} notes ready, offset={_offsetSeconds:F2}s ({offsetFrames}f)");
        }

        private IEnumerator WarmUpAndDeactivateNextFrame(List<(GameObject obj, string preset)> spawned)
        {
            yield return null;

            foreach (var (obj, preset) in spawned)
                obj.GetComponent<IndicatorController>()?.WarmUp(preset);

            foreach (var (obj, _) in spawned)
                obj.SetActive(false);
        }

        public void SetVideoPlayer(VideoPlayer vp)
        {
            _videoPlayer = vp;
        }

        public void StartGame()
        {
            _fallbackTime = 0f;
            _isRunning    = true;
        }

        private void Update()
        {
            if (!_isRunning) return;

            if (_videoPlayer == null || !_videoPlayer.isPlaying)
                _fallbackTime += Time.deltaTime;

            float t = GameTime;

            // オフセット期間中（映像開始直後）はスポーンしない
            if (t < _offsetSeconds) return;

            // オフセット後の経過時間でノーツタイミングを判定
            float gameT = t - _offsetSeconds;

            while (_pendingNotes.Count > 0)
            {
                var (note, preset, color) = _pendingNotes.Peek();
                if (note.time - gameT > spawnLeadTime) break;

                _pendingNotes.Dequeue();
                SpawnNote(note, preset, color, gameT);
            }

            if (_pendingNotes.Count == 0 && _pool.Count == _allViews.Count)
            {
                _isRunning = false;
                uiManager?.SubmitFinalScore();
            }
        }

        private void SpawnNote(NoteData note, string preset, ColorData color, float gameT)
        {
            if (_pool.Count == 0)
            {
                Debug.LogWarning("[NotesRunner] プールが枯渇しました");
                return;
            }

            NoteView view = _pool.Dequeue();
            view.gameObject.SetActive(true);
            view.Setup(note, preset, color, note.time - gameT, inputHandler, uiManager, this);
        }

        // NoteViewからプールに返却される
        public void ReturnToPool(NoteView view)
        {
            view.gameObject.SetActive(false);
            _pool.Enqueue(view);
        }
    }
}
