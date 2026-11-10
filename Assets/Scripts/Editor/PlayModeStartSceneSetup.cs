using UnityEditor;
using UnityEditor.SceneManagement;

namespace Mophia.EditorTools
{
    /// <summary>
    /// Editorで現在どのシーンを開いていても、Playボタンを押したら必ずTitleから始まるようにする。
    /// (未設定だと、直前に開いていたシーン(例: SongSelect)がそのままPlayされてしまう)
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeStartSceneSetup
    {
        private const string TitleScenePath = "Assets/Scenes/Title.unity";

        static PlayModeStartSceneSetup()
        {
            if (EditorSceneManager.playModeStartScene != null) return;

            var titleScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
            if (titleScene != null)
                EditorSceneManager.playModeStartScene = titleScene;
        }
    }
}
