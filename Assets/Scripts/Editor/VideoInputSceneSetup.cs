using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Mophia.Core;
using Mophia.UI;

namespace Mophia.EditorTools
{
    /// <summary>
    /// 動画入力画面(動画を選ぶ → 変換の進捗バーを表示 → 完了後SongSelectへ)を新規作成する。
    /// Titleのuploadボタンから遷移してくる。
    /// </summary>
    public static class VideoInputSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/VideoInput.unity";

        [MenuItem("Mophia/動画入力シーンを作成する")]
        public static void CreateScene()
        {
            AssetDatabase.Refresh();

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

            // 戻るボタン(左上)
            var backGo = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backGo.transform.SetParent(canvasGo.transform, false);
            var backRt = (RectTransform)backGo.transform;
            backRt.anchorMin = new Vector2(0, 1);
            backRt.anchorMax = new Vector2(0, 1);
            backRt.pivot     = new Vector2(0, 1);
            backRt.anchoredPosition = new Vector2(40, -40);
            backRt.sizeDelta = new Vector2(160, 130);
            backGo.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(backRt, backRt.sizeDelta, thickness: 4f, color: Color.black);
            UIBuild.Text(backGo.transform, "Label", "<", agencyBold, 40, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: new Vector2(160, 130),
                alignment: TextAlignmentOptions.Center);

            var navGo = new GameObject("SceneNav", typeof(SceneNav));
            var nav = navGo.GetComponent<SceneNav>();
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                backGo.GetComponent<Button>().onClick, nav.LoadScene, "Title");

            UIBuild.Text(canvasGo.transform, "Heading", "動画をアップロード", yuGothic, 28, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, 180), size: new Vector2(900, 40),
                alignment: TextAlignmentOptions.Center);

            Button selectButton = UIBuild.BracketButton(canvasGo.transform, "SelectButton", "select video", agencyBold,
                pos: new Vector2(0, 40), size: new Vector2(440, 100));

            TMP_Text statusText = UIBuild.Text(canvasGo.transform, "StatusText", "", yuGothic, 20, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, -100), size: new Vector2(900, 40),
                alignment: TextAlignmentOptions.Center);

            // 進捗バー(枠 + 塗りつぶし)
            var barBackGo = new GameObject("ProgressBarBack", typeof(RectTransform), typeof(Image));
            barBackGo.transform.SetParent(canvasGo.transform, false);
            var barBackRt = (RectTransform)barBackGo.transform;
            barBackRt.anchorMin = new Vector2(0.5f, 0.5f);
            barBackRt.anchorMax = new Vector2(0.5f, 0.5f);
            barBackRt.pivot     = new Vector2(0.5f, 0.5f);
            barBackRt.anchoredPosition = new Vector2(0, -160);
            barBackRt.sizeDelta = new Vector2(700, 30);
            barBackGo.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(barBackRt, barBackRt.sizeDelta, thickness: 3f, color: Color.black);

            var barFillGo = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barBackGo.transform, false);
            var barFillRt = (RectTransform)barFillGo.transform;
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = Vector2.one;
            barFillRt.offsetMin = new Vector2(4, 4);
            barFillRt.offsetMax = new Vector2(-4, -4);
            Image progressBarFill = barFillGo.GetComponent<Image>();
            // Image.Type.Filledはsprite未設定だとfillAmountを無視して常に全面描画されるため、
            // Unity標準のUIスプライトを明示的に割り当てる
            progressBarFill.sprite      = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            progressBarFill.color       = Color.black;
            progressBarFill.type        = Image.Type.Filled;
            progressBarFill.fillMethod  = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin  = (int)Image.OriginHorizontal.Left;
            progressBarFill.fillAmount  = 0f;

            TMP_Text progressPercentText = UIBuild.Text(canvasGo.transform, "ProgressPercentText", "0%", agencyBold, 22, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, -200), size: new Vector2(700, 30),
                alignment: TextAlignmentOptions.Center);

            var converterGo = new GameObject("SongConverter", typeof(SongConverter));
            var converter = converterGo.GetComponent<SongConverter>();
            var converterSo = new SerializedObject(converter);
            converterSo.FindProperty("statusText").objectReferenceValue          = statusText;
            converterSo.FindProperty("progressBarFill").objectReferenceValue     = progressBarFill;
            converterSo.FindProperty("progressPercentText").objectReferenceValue = progressPercentText;
            converterSo.FindProperty("convertButton").objectReferenceValue       = selectButton;
            converterSo.ApplyModifiedProperties();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(selectButton.onClick, converter.PickAndConvert);

            // スマホからのQRアップロード
            UIBuild.Text(canvasGo.transform, "QrLabel", "またはスマホから動画を送る", yuGothic, 18, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, -250), size: new Vector2(900, 30),
                alignment: TextAlignmentOptions.Center);

            var qrGo = new GameObject("QrImage", typeof(RectTransform), typeof(RawImage));
            qrGo.transform.SetParent(canvasGo.transform, false);
            var qrRt = (RectTransform)qrGo.transform;
            qrRt.anchorMin = new Vector2(0.5f, 0.5f);
            qrRt.anchorMax = new Vector2(0.5f, 0.5f);
            qrRt.pivot     = new Vector2(0.5f, 0.5f);
            qrRt.anchoredPosition = new Vector2(0, -355);
            qrRt.sizeDelta = new Vector2(170, 170);
            var qrImage = qrGo.GetComponent<RawImage>();
            qrImage.color = Color.white; // QR生成前のプレースホルダー(生成後はテクスチャがそのまま見た目になる)
            UIBuild.AddBorderFrame(qrRt, qrRt.sizeDelta, thickness: 3f, color: Color.black);

            TMP_Text urlText = UIBuild.Text(canvasGo.transform, "UrlText", "", agencyBold, 18, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: new Vector2(0, -460), size: new Vector2(900, 26),
                alignment: TextAlignmentOptions.Center);

            // PCのネットワーク接続先が変わるとcloudflaredのトンネルが復帰しないことがあるための手動リカバリ。
            // BracketButtonは外側の角飾りが大きく、この位置だとURLテキストや画面下端と干渉するため
            // 装飾のないシンプルな枠ボタンにしている
            var reconnectGo = new GameObject("ReconnectButton", typeof(RectTransform), typeof(Image), typeof(Button));
            reconnectGo.transform.SetParent(canvasGo.transform, false);
            var reconnectRt = (RectTransform)reconnectGo.transform;
            reconnectRt.anchorMin = new Vector2(0.5f, 0.5f);
            reconnectRt.anchorMax = new Vector2(0.5f, 0.5f);
            reconnectRt.pivot     = new Vector2(0.5f, 0.5f);
            reconnectRt.anchoredPosition = new Vector2(0, -507);
            reconnectRt.sizeDelta = new Vector2(200, 40);
            reconnectGo.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(reconnectRt, reconnectRt.sizeDelta, thickness: 3f, color: Color.black);
            UIBuild.Text(reconnectGo.transform, "Label", "reconnect", yuGothic, 18, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: new Vector2(200, 40),
                alignment: TextAlignmentOptions.Center);
            Button reconnectButton = reconnectGo.GetComponent<Button>();

            var serverGo = new GameObject("VideoUploadServer", typeof(VideoUploadServer));
            var uploadServer = serverGo.GetComponent<VideoUploadServer>();

            var screenGo = new GameObject("VideoInputScreen", typeof(VideoInputScreen));
            var videoInputScreen = screenGo.GetComponent<VideoInputScreen>();
            var screenSo = new SerializedObject(videoInputScreen);
            screenSo.FindProperty("uploadServer").objectReferenceValue = uploadServer;
            screenSo.FindProperty("songConverter").objectReferenceValue = converter;
            screenSo.FindProperty("qrImage").objectReferenceValue = qrImage;
            screenSo.FindProperty("urlText").objectReferenceValue = urlText;
            screenSo.ApplyModifiedProperties();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(reconnectButton.onClick, videoInputScreen.Reconnect);

            UIBuild.ApplyLetterbox(canvasGo);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettingsIfMissing(ScenePath);

            Debug.Log($"[VideoInputSceneSetup] 完了: {ScenePath} を作成しました");
        }

        private static void AddToBuildSettingsIfMissing(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;

            int titleIndex = scenes.FindIndex(s => s.path == "Assets/Scenes/Title.unity");
            int insertAt   = titleIndex >= 0 ? titleIndex + 1 : scenes.Count;
            scenes.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
