using System.IO;
using UnityEngine;

namespace Mophia.Core
{
    /// <summary>
    /// 選択中の曲をシーンをまたいで保持するための入れ物。
    /// 曲は StreamingAssets/Songs/&lt;SelectedSongFolder&gt;/ に入っている前提。
    /// </summary>
    public static class SongSelection
    {
        public const string SongsRootFolderName = "Songs";

        // GamePlayシーンを選択画面を経由せず単体テストする場合のデフォルト
        public static string SelectedSongFolder = "motion_study_1";

        public static string SongsRootPath =>
            Path.Combine(Application.streamingAssetsPath, SongsRootFolderName);

        public static string GetSongDirectory() =>
            Path.Combine(SongsRootPath, SelectedSongFolder);
    }
}
