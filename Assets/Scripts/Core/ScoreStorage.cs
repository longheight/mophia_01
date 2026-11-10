using UnityEngine;

namespace Mophia.Core
{
    /// <summary>
    /// 曲ごとのベストスコアをローカルに保存する。
    /// </summary>
    public static class ScoreStorage
    {
        public static int GetBestScore(string songFolder) =>
            PlayerPrefs.GetInt(Key(songFolder), 0);

        public static void SubmitScore(string songFolder, int score)
        {
            if (string.IsNullOrEmpty(songFolder)) return;
            if (score > GetBestScore(songFolder))
            {
                PlayerPrefs.SetInt(Key(songFolder), score);
                PlayerPrefs.Save();
            }
        }

        public static void DeleteScore(string songFolder)
        {
            if (string.IsNullOrEmpty(songFolder)) return;
            PlayerPrefs.DeleteKey(Key(songFolder));
            PlayerPrefs.Save();
        }

        public static string GetRank(int score)
        {
            if (score <= 0)      return "-";
            if (score >= 950000) return "S";
            if (score >= 850000) return "A";
            if (score >= 700000) return "B";
            return "C";
        }

        private static string Key(string songFolder) => $"mophia_score_{songFolder}";
    }
}
