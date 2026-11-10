using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Mophia.Data;

namespace Mophia.Core
{
    /// <summary>
    /// StreamingAssets/Songs 配下をスキャンして選択リストを作り、選んだ曲の詳細
    /// (配色・ベストスコア/ランク)を右側パネルに表示する。
    /// </summary>
    public class SongSelectManager : MonoBehaviour
    {
        private class SongInfo
        {
            public string folderName;
            public string title;
            public string mp4FileName;
            public Color  primary;
            public Color  secondary;
            public bool   hasColor;
        }

        [Header("リスト")]
        [SerializeField] private Transform    listContent;
        [SerializeField] private TMP_FontAsset entryFont;
        [SerializeField] private string        gamePlaySceneName = "GamePlay";

        [Header("詳細パネル")]
        [SerializeField] private TMP_Text detailTitleText;
        [SerializeField] private Image    colorSwatch1;
        [SerializeField] private Image    colorSwatch2;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private RawImage thumbnailVideoDisplay;

        [Header("削除確認ポップアップ")]
        [SerializeField] private GameObject deleteConfirmPanel;
        [SerializeField] private TMP_Text   deleteConfirmSongNameText;

        private readonly List<SongInfo> _songs = new List<SongInfo>();
        private readonly Dictionary<SongInfo, TMP_Text> _entryLabels = new Dictionary<SongInfo, TMP_Text>();
        private SongInfo _selected;

        // 詳細パネルのサムネイルプレビュー用。常に選択中の曲1本分しか再生しない(常時1インスタンスのみ)
        private VideoPlayer      _thumbnailPlayer;
        private RenderTexture    _thumbnailRt;
        private AspectRatioFitter _thumbnailAspectFitter;
        private int               _thumbnailRequestId;    // 選択が高速に切り替わったときの取り違え防止
        private int               _thumbnailPreparingId = -1;

        private void Start()
        {
            PopulateList();
        }

        // 変換完了後などにリストを作り直す
        public void Refresh()
        {
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);
            _songs.Clear();
            _entryLabels.Clear();
            _selected = null;

            PopulateList();
        }

        private void PopulateList()
        {
            string root = SongSelection.SongsRootPath;
            if (!Directory.Exists(root))
            {
                Debug.LogError($"[SongSelectManager] Songsフォルダが見つかりません: {root}");
                return;
            }

            foreach (string dir in Directory.GetDirectories(root))
            {
                string folderName = Path.GetFileName(dir);
                string chartPath  = Path.Combine(dir, "chart.json");
                if (!File.Exists(chartPath)) continue;

                var info = new SongInfo { folderName = folderName, title = folderName };
                try
                {
                    ChartData chart = JsonUtility.FromJson<ChartData>(File.ReadAllText(chartPath));
                    if (chart?.meta != null && !string.IsNullOrEmpty(chart.meta.title))
                        info.title = chart.meta.title;
                    if (chart?.meta != null) info.mp4FileName = chart.meta.mp4;

                    if (chart?.sections != null && chart.sections.Count > 0 && chart.sections[0].color != null)
                    {
                        info.primary   = chart.sections[0].color.PrimaryColor;
                        info.secondary = chart.sections[0].color.SecondaryColor;
                        info.hasColor  = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SongSelectManager] {chartPath} の読み込みに失敗、フォルダ名をタイトルに使います: {e.Message}");
                }

                _songs.Add(info);
                CreateEntry(info);
            }

            if (_songs.Count > 0)
            {
                // リザルト画面から戻ってきた場合など、直前にプレイしていた曲があればそれを選択したままにする
                SongInfo lastPlayed = _songs.Find(s => s.folderName == SongSelection.SelectedSongFolder);
                Select(lastPlayed ?? _songs[0]);
            }
        }

        private void CreateEntry(SongInfo info)
        {
            var go = new GameObject($"Song_{info.folderName}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(listContent, false);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 70;
            layoutElement.flexibleWidth   = 1;

            var bg = go.GetComponent<Image>();
            bg.color = Color.white;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(24, 0);
            textRt.offsetMax = new Vector2(-24, 0);

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text      = info.title;
            if (entryFont != null) text.font = entryFont;
            text.fontSize  = 28;
            text.alignment = TextAlignmentOptions.Left;
            text.color     = Color.black;

            _entryLabels[info] = text;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => Select(info));
        }

        private void Select(SongInfo info)
        {
            _selected = info;
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);

            foreach (var kv in _entryLabels)
            {
                bool isSelected = kv.Key == info;
                kv.Value.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
                kv.Value.fontSize  = isSelected ? 34 : 28;
            }

            if (detailTitleText != null) detailTitleText.text = info.title;
            if (colorSwatch1 != null) colorSwatch1.color = info.hasColor ? info.primary   : new Color(0, 0, 0, 0);
            if (colorSwatch2 != null) colorSwatch2.color = info.hasColor ? info.secondary : new Color(0, 0, 0, 0);

            int best = ScoreStorage.GetBestScore(info.folderName);
            if (scoreText != null) scoreText.text = best > 0 ? $"{best}" : "-";
            if (rankText  != null) rankText.text  = ScoreStorage.GetRank(best);

            ApplyThumbnail(info);
        }

        // 選択中の曲の動画をミュートでループ再生するプレビュー。常に1本(直前の選択)だけを再生するので、
        // 曲数が増えても重くならない。読み込み中/動画が無い場合は背景のプレースホルダー(枠)が見える。
        private void ApplyThumbnail(SongInfo info)
        {
            if (thumbnailVideoDisplay == null) return;

            EnsureThumbnailPlayer();
            if (_thumbnailAspectFitter == null)
                _thumbnailAspectFitter = thumbnailVideoDisplay.GetComponent<AspectRatioFitter>();

            _thumbnailRequestId++;
            thumbnailVideoDisplay.gameObject.SetActive(false);
            _thumbnailPlayer.Stop();

            string videoPath = !string.IsNullOrEmpty(info.mp4FileName)
                ? Path.Combine(SongSelection.SongsRootPath, info.folderName, info.mp4FileName)
                : null;
            if (videoPath == null || !File.Exists(videoPath)) return;

            _thumbnailPreparingId = _thumbnailRequestId;
            _thumbnailPlayer.url = "file://" + videoPath;
            _thumbnailPlayer.Prepare();
        }

        private void EnsureThumbnailPlayer()
        {
            if (_thumbnailPlayer != null) return;

            var go = new GameObject("ThumbnailPreviewPlayer", typeof(VideoPlayer));
            go.transform.SetParent(transform, false);

            _thumbnailPlayer = go.GetComponent<VideoPlayer>();
            _thumbnailPlayer.playOnAwake     = false;
            _thumbnailPlayer.isLooping       = true;
            _thumbnailPlayer.renderMode      = VideoRenderMode.RenderTexture;
            _thumbnailPlayer.audioOutputMode = VideoAudioOutputMode.None; // 一覧のプレビューなので無音
            _thumbnailPlayer.prepareCompleted += OnThumbnailPrepared;
        }

        private void OnThumbnailPrepared(VideoPlayer vp)
        {
            // 準備中に別の曲が選択されていたら、この結果は古いので無視する
            if (_thumbnailPreparingId != _thumbnailRequestId) return;

            if (_thumbnailRt == null || _thumbnailRt.width != vp.width || _thumbnailRt.height != vp.height)
            {
                if (_thumbnailRt != null) _thumbnailRt.Release();
                _thumbnailRt = new RenderTexture((int)vp.width, (int)vp.height, 0);
                vp.targetTexture = _thumbnailRt;
            }

            if (_thumbnailAspectFitter != null && vp.height > 0)
                _thumbnailAspectFitter.aspectRatio = (float)vp.width / vp.height;

            thumbnailVideoDisplay.texture = _thumbnailRt;
            thumbnailVideoDisplay.gameObject.SetActive(true);
            vp.Play();
        }

        private void OnDestroy()
        {
            if (_thumbnailRt != null) _thumbnailRt.Release();
        }

        // playボタンから呼ぶ
        public void PlaySelected()
        {
            if (_selected == null) return;
            SongSelection.SelectedSongFolder = _selected.folderName;
            SceneManager.LoadScene(gamePlaySceneName);
        }

        // 削除ボタンから呼ぶ。確認ポップアップを表示する(実際の削除はConfirmDeleteから)
        public void OnDeleteButtonClicked()
        {
            if (_selected == null) return;

            if (deleteConfirmSongNameText != null) deleteConfirmSongNameText.text = _selected.title;
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(true);
        }

        // 確認ポップアップの「はい」から呼ぶ
        public void ConfirmDelete()
        {
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
            if (_selected != null) DeleteSong(_selected);
        }

        // 確認ポップアップの「いいえ」から呼ぶ
        public void CancelDelete()
        {
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        }

        private void DeleteSong(SongInfo info)
        {
            // 動画ファイルを掴んだままだと削除できないので、プレビューを先に止めて解放する
            if (_thumbnailPlayer != null)
            {
                _thumbnailPlayer.Stop();
                _thumbnailPlayer.url = "";
            }
            if (thumbnailVideoDisplay != null) thumbnailVideoDisplay.gameObject.SetActive(false);

            string folderPath = Path.Combine(SongSelection.SongsRootPath, info.folderName);
            try
            {
                if (Directory.Exists(folderPath)) Directory.Delete(folderPath, recursive: true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SongSelectManager] 削除に失敗しました: {folderPath} ({e.Message})");
                return;
            }

            ScoreStorage.DeleteScore(info.folderName);
            Refresh();
        }
    }
}
