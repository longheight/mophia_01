using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mophia.Core;
using Mophia.Input;

namespace Mophia.UI
{
    /// <summary>
    /// 画面全体のUI管理。スコア表示・コンボ表示など。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private const int MaxScore = 1000000;
        private const float PopupDuration = 0.6f;
        private const float PopupRiseDistance = 60f;
        private const float PopupFontSize = 42f;

        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private ResultPanel resultPanel;

        private int _score;
        private int _combo;
        private int _totalNotes = 1;

        // 曲全体のノーツ数が分かってから呼ぶ(1ノーツあたりの満点を決めるため)
        public void SetTotalNotes(int totalNotes)
        {
            _totalNotes = Mathf.Max(1, totalNotes);
        }

        // NoteView.OnInputから判定結果ごとに呼ばれ、スコア/コンボの計算と表示更新をまとめて行う。
        // noteRectはPERFECT/GOOD/BAD/MISSのポップアップ表示位置(押したノーツの位置)に使う。
        // どれが正しく押せたか/押せていないかを見た目で分からせるための唯一の手段なので必須。
        public void RegisterJudgment(JudgeResult result, RectTransform noteRect)
        {
            _score += ScoreFor(result);
            _combo = result == JudgeResult.Miss ? 0 : _combo + 1;

            UpdateScore(_score);
            UpdateCombo(_combo);
            ShowJudgmentPopup(result, noteRect);
        }

        private void ShowJudgmentPopup(JudgeResult result, RectTransform noteRect)
        {
            if (noteRect == null || noteRect.parent == null) return;

            var go = new GameObject("JudgmentPopup", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(noteRect.parent, false);
            rect.anchoredPosition = noteRect.anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 80f);

            var text = go.AddComponent<Text>();
            // 日本語が正しく表示される既存のフォント(scoreText等に設定済み)をそのまま流用する。
            // TMPのデフォルトフォント(LiberationSans SDF)は日本語グリフを含まず豆腐化するため使わない。
            text.font        = scoreText != null ? scoreText.font : comboText != null ? comboText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.alignment    = TextAnchor.MiddleCenter;
            text.fontSize     = (int)PopupFontSize;
            text.fontStyle    = FontStyle.Bold;
            text.text         = LabelFor(result);
            text.color        = ColorFor(result);
            text.raycastTarget = false;

            StartCoroutine(AnimatePopup(rect, text));
        }

        private static string LabelFor(JudgeResult result) => result switch
        {
            JudgeResult.Perfect => "Perfect",
            JudgeResult.Great   => "Great",
            JudgeResult.Good    => "Good",
            JudgeResult.Bad     => "Bad",
            _                   => "Miss",
        };

        private static Color ColorFor(JudgeResult result) => result switch
        {
            JudgeResult.Perfect => new Color(1f,   0.85f, 0.2f),
            JudgeResult.Great   => new Color(0.6f, 1f,    0.4f),
            JudgeResult.Good    => new Color(0.4f, 0.9f,  1f),
            JudgeResult.Bad     => new Color(0.8f, 0.8f,  0.8f),
            _                   => new Color(1f,   0.3f,  0.3f),
        };

        private static IEnumerator AnimatePopup(RectTransform rect, Text text)
        {
            Vector2 startPos = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < PopupDuration)
            {
                float t = elapsed / PopupDuration;
                rect.anchoredPosition = startPos + Vector2.up * (PopupRiseDistance * t);
                Color c = text.color;
                text.color = new Color(c.r, c.g, c.b, 1f - t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Destroy(rect.gameObject);
        }

        private int ScoreFor(JudgeResult result)
        {
            int perNoteMax = MaxScore / _totalNotes;
            return result switch
            {
                JudgeResult.Perfect => perNoteMax,
                JudgeResult.Great   => (int)(perNoteMax * 0.85f),
                JudgeResult.Good    => (int)(perNoteMax * 0.7f),
                JudgeResult.Bad     => (int)(perNoteMax * 0.3f),
                _                   => 0,
            };
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null) scoreText.text = $"{score}";
        }

        public void UpdateCombo(int combo)
        {
            if (comboText != null) comboText.text = combo > 0 ? $"{combo} COMBO" : "";
        }

        // 曲が最後まで終わったときに呼ばれ、ベストスコアを保存してリザルトを表示する
        public void SubmitFinalScore()
        {
            ScoreStorage.SubmitScore(SongSelection.SelectedSongFolder, _score);
            resultPanel?.Show(_score);
        }
    }
}
