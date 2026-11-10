using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mophia.UI
{
    /// <summary>曲が終わったときに表示する結果パネル(スコア/ランク + 曲選択に戻るボタン)。</summary>
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text   scoreText;
        [SerializeField] private TMP_Text   rankText;
        [SerializeField] private string     songSelectSceneName = "SongSelect";

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Show(int score)
        {
            if (scoreText != null) scoreText.text = $"{score}";
            if (rankText  != null) rankText.text  = Mophia.Core.ScoreStorage.GetRank(score);
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        // 「曲選択に戻る」ボタンから呼ぶ
        public void BackToSongSelect()
        {
            SceneManager.LoadScene(songSelectSceneName);
        }
    }
}
