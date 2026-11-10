using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Mophia.Core;

namespace Mophia.EditorTools
{
    /// <summary>
    /// タイトル画面(ロゴ + play/upload)を新規作成する。
    /// </summary>
    public static class TitleSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Title.unity";

        [MenuItem("Mophia/タイトルシーンを作成する")]
        public static void CreateScene()
        {
            AssetDatabase.Refresh(); // Assets/Fonts配下のフォントがまだ未インポートの場合に備えて

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags      = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black; // レターボックス外周の色。内側の白背景はLetterboxRootが担う
            camera.orthographic    = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            TMP_FontAsset agencyBold = UIBuild.GetOrCreateFontAsset("Assets/Fonts/AgencyFB-Bold SDF.asset", "Assets/Fonts/AGENCYB.TTF");
            TMP_FontAsset yuGothic   = UIBuild.GetOrCreateFontAsset("Assets/Fonts/YuGothic-Regular SDF.asset", "Assets/Fonts/YuGothR.ttc");
            UIBuild.SetFallback(agencyBold, yuGothic);

            UIBuild.Text(canvasGo.transform, "Logo", "mophia", agencyBold, 96, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, 100), size: new Vector2(900, 140),
                alignment: TextAlignmentOptions.Center);

            UIBuild.Text(canvasGo.transform, "Subtitle", "動画をアップロードして譜面を生成します。", yuGothic, 22, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, 10), size: new Vector2(900, 40),
                alignment: TextAlignmentOptions.Center);

            Button playButton = UIBuild.BracketButton(canvasGo.transform, "PlayButton", "play", agencyBold,
                pos: new Vector2(-260, -140), size: new Vector2(420, 90));

            Button uploadButton = UIBuild.BracketButton(canvasGo.transform, "UploadButton", "upload", agencyBold,
                pos: new Vector2(260, -140), size: new Vector2(420, 90));

            var navGo = new GameObject("SceneNav", typeof(SceneNav));
            var nav = navGo.GetComponent<SceneNav>();

            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(playButton.onClick, nav.LoadScene, "SongSelect");
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(uploadButton.onClick, nav.LoadScene, "VideoInput");

            UIBuild.ApplyLetterbox(canvasGo);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettingsFirst(ScenePath);

            Debug.Log($"[TitleSceneSetup] 完了: {ScenePath} を作成しました");
        }

        private static void AddToBuildSettingsFirst(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
