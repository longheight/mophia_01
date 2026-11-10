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
    /// 曲選択/詳細画面(左: 曲リスト、右: 配色・サムネ・スコア/ランク・play)を新規作成する。
    /// </summary>
    public static class SongSelectSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SongSelect.unity";

        [MenuItem("Mophia/曲選択シーンを作成する(新デザイン)")]
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

            TMP_FontAsset agency = UIBuild.GetOrCreateFontAsset("Assets/Fonts/AgencyFB-Regular SDF.asset", "Assets/Fonts/AGENCYR.TTF");

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
            UIBuild.Text(backGo.transform, "Label", "<", agency, 40, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: new Vector2(160, 130),
                alignment: TextAlignmentOptions.Center);

            var navGo = new GameObject("SceneNav", typeof(SceneNav));
            var nav = navGo.GetComponent<SceneNav>();
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                backGo.GetComponent<Button>().onClick, nav.LoadScene, "Title");

            // 曲リスト(左)。戻るボタンの下から始まるようにする
            var scrollGo = new GameObject("SongScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(canvasGo.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(0f, 1f);
            scrollRt.pivot     = new Vector2(0f, 1f);
            scrollRt.anchoredPosition = new Vector2(40, -200);
            scrollRt.sizeDelta = new Vector2(600, -280);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.03f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0, 0);

            var layoutGroup = contentGo.GetComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 14;
            layoutGroup.padding = new RectOffset(10, 10, 10, 10);
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth  = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth  = true;

            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // スクロールバー(縦)。曲が枠に収まる間は自動で隠れる(AutoHideAndExpandViewport)
            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollGo.transform, false);
            var scrollbarRt = (RectTransform)scrollbarGo.transform;
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot     = new Vector2(1f, 0.5f);
            scrollbarRt.anchoredPosition = Vector2.zero;
            scrollbarRt.sizeDelta = new Vector2(10, 0);
            scrollbarGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.08f); // トラック

            var handleAreaGo = new GameObject("SlidingArea", typeof(RectTransform));
            handleAreaGo.transform.SetParent(scrollbarGo.transform, false);
            var handleAreaRt = (RectTransform)handleAreaGo.transform;
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(2, 2);
            handleAreaRt.offsetMax = new Vector2(-2, -2);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = new Vector2(1f, 0.2f);
            handleRt.sizeDelta = Vector2.zero;
            Image handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(0, 0, 0, 0.5f);

            Scrollbar scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.handleRect    = handleRt;
            scrollbar.direction     = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.viewport   = viewportRt;
            scrollRect.content    = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;
            scrollRect.verticalScrollbar             = scrollbar;
            scrollRect.verticalScrollbarVisibility   = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing      = -4f;

            // 詳細パネル(右)。1920幅に対して右端80pxマージンまで埋める
            float rightX     = 700f;
            float rightWidth = 1920f - 80f - rightX; // = 1140

            TMP_Text titleText = UIBuild.Text(canvasGo.transform, "DetailTitle", "", agency, 40, Color.black,
                pivot: new Vector2(0f, 1f), pos: new Vector2(rightX, -50), size: new Vector2(rightWidth, 60),
                alignment: TextAlignmentOptions.Left);

            UIBuild.Text(canvasGo.transform, "StageColorsLabel", "stage colors", agency, 20, Color.black,
                pivot: new Vector2(0f, 1f), pos: new Vector2(rightX, -130), size: new Vector2(300, 30),
                alignment: TextAlignmentOptions.Left);

            Image swatch1 = CreateSwatch(canvasGo.transform, "Swatch1", new Vector2(rightX, -170), true);
            Image swatch2 = CreateSwatch(canvasGo.transform, "Swatch2", new Vector2(rightX + 70, -170), true);
            for (int i = 0; i < 4; i++)
                CreateSwatch(canvasGo.transform, $"SwatchEmpty{i}", new Vector2(rightX + 140 + i * 70, -170), false);

            // 削除ボタン(押すと確認ポップアップが開く)。stage colors行と同じ高さ、右端に置く
            TMP_FontAsset yuGothic = UIBuild.GetOrCreateFontAsset("Assets/Fonts/YuGothic-Regular SDF.asset", "Assets/Fonts/YuGothR.ttc");
            // 楽曲タイトルはユーザー入力(日本語のことがある)なので、欧文フォントに和文フォールバックを設定しておく
            UIBuild.SetFallback(agency, yuGothic);
            var deleteGo = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            deleteGo.transform.SetParent(canvasGo.transform, false);
            var deleteRt = (RectTransform)deleteGo.transform;
            deleteRt.anchorMin = new Vector2(1f, 1f);
            deleteRt.anchorMax = new Vector2(1f, 1f);
            deleteRt.pivot     = new Vector2(1f, 1f);
            deleteRt.anchoredPosition = new Vector2(-80, -170);
            deleteRt.sizeDelta = new Vector2(140, 50);
            deleteGo.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(deleteRt, deleteRt.sizeDelta, thickness: 3f, color: Color.black);
            UIBuild.Text(deleteGo.transform, "Label", "削除", yuGothic, 20, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: new Vector2(140, 50),
                alignment: TextAlignmentOptions.Center);
            Button deleteButton = deleteGo.GetComponent<Button>();

            // サムネイルのダミー枠
            var thumbGo = new GameObject("ThumbnailPlaceholder", typeof(RectTransform), typeof(Image));
            thumbGo.transform.SetParent(canvasGo.transform, false);
            var thumbRt = (RectTransform)thumbGo.transform;
            thumbRt.anchorMin = new Vector2(0f, 1f);
            thumbRt.anchorMax = new Vector2(0f, 1f);
            thumbRt.pivot     = new Vector2(0f, 1f);
            thumbRt.anchoredPosition = new Vector2(rightX, -240);
            thumbRt.sizeDelta = new Vector2(rightWidth, 460);
            Image thumbnailImage = thumbGo.GetComponent<Image>();
            thumbnailImage.color = new Color(0.92f, 0.92f, 0.92f, 1f); // 背景パーティクルが透けないよう不透明にする(動画プレビュー読み込み中/無い場合のフォールバック)

            // 選択中の曲をミュートでループ再生するプレビュー。読み込み前は非表示にして上のグレー枠を見せる。
            // AspectRatioFitter(FitInParent)で元動画のアスペクト比を保ったまま枠内に収める
            var thumbVideoGo = new GameObject("ThumbnailVideoDisplay", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            thumbVideoGo.transform.SetParent(thumbGo.transform, false);
            var thumbVideoRt = (RectTransform)thumbVideoGo.transform;
            thumbVideoRt.anchorMin = Vector2.zero;
            thumbVideoRt.anchorMax = Vector2.one;
            thumbVideoRt.offsetMin = Vector2.zero;
            thumbVideoRt.offsetMax = Vector2.zero;
            RawImage thumbnailVideoDisplay = thumbVideoGo.GetComponent<RawImage>();
            thumbVideoGo.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            thumbVideoGo.SetActive(false);

            // 枠線は動画プレビューよりあとに追加する(先に追加すると、レターボックスで隙間ができない
            // 側の辺が動画に隠れてしまうため。あとから追加した方が描画順で手前に来る)
            UIBuild.AddBorderFrame(thumbRt, thumbRt.sizeDelta, thickness: 4f, color: Color.black);

            // スコア/ランク
            UIBuild.Text(canvasGo.transform, "ScoreLabel", "score", agency, 20, Color.black,
                pivot: new Vector2(0f, 1f), pos: new Vector2(rightX, -750), size: new Vector2(200, 30),
                alignment: TextAlignmentOptions.Left);

            TMP_Text scoreText = UIBuild.Text(canvasGo.transform, "ScoreText", "-", agency, 48, Color.black,
                pivot: new Vector2(0f, 1f), pos: new Vector2(rightX, -810), size: new Vector2(rightWidth - 130, 60),
                alignment: TextAlignmentOptions.Left);

            // 目立たせすぎないよう、スコアより控えめなサイズにする。
            // pivot(1,1)は画面右上を基準にした座標なので、右マージン(1920-80=1840 → オフセットは-80)で指定する
            TMP_Text rankText = UIBuild.Text(canvasGo.transform, "RankText", "-", agency, 34, Color.black,
                pivot: new Vector2(1f, 1f), pos: new Vector2(-80, -818), size: new Vector2(120, 40),
                alignment: TextAlignmentOptions.Right);

            // score/rank行の下線(スコア/ランクの文字と重ならないよう、両方の下端より下に置く)
            var underlineGo = new GameObject("ScoreUnderline", typeof(RectTransform), typeof(Image));
            underlineGo.transform.SetParent(canvasGo.transform, false);
            var underlineRt = (RectTransform)underlineGo.transform;
            underlineRt.anchorMin = new Vector2(0f, 1f);
            underlineRt.anchorMax = new Vector2(0f, 1f);
            underlineRt.pivot     = new Vector2(0f, 1f);
            underlineRt.anchoredPosition = new Vector2(rightX, -895);
            underlineRt.sizeDelta = new Vector2(rightWidth, 3f);
            underlineGo.GetComponent<Image>().color = Color.black;

            // playボタン
            Button playButton = UIBuild.BracketButton(canvasGo.transform, "PlayButton", "play", agency,
                pos: new Vector2(0, 0), size: new Vector2(rightWidth - 40, 90));
            var playRt = (RectTransform)playButton.transform;
            playRt.anchorMin = new Vector2(0f, 1f);
            playRt.anchorMax = new Vector2(0f, 1f);
            playRt.pivot     = new Vector2(0f, 1f);
            playRt.anchoredPosition = new Vector2(rightX + 20, -930);

            // 削除確認ポップアップ(削除ボタンから開く。はい/いいえで応答するまで背後は暗幕で覆う)
            var deleteConfirmGo = new GameObject("DeleteConfirmPanel", typeof(RectTransform), typeof(Image));
            deleteConfirmGo.transform.SetParent(canvasGo.transform, false);
            var deleteConfirmRt = (RectTransform)deleteConfirmGo.transform;
            deleteConfirmRt.anchorMin = Vector2.zero;
            deleteConfirmRt.anchorMax = Vector2.one;
            deleteConfirmRt.offsetMin = Vector2.zero;
            deleteConfirmRt.offsetMax = Vector2.zero;
            deleteConfirmGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var deleteBoxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            deleteBoxGo.transform.SetParent(deleteConfirmGo.transform, false);
            var deleteBoxRt = (RectTransform)deleteBoxGo.transform;
            deleteBoxRt.anchorMin = new Vector2(0.5f, 0.5f);
            deleteBoxRt.anchorMax = new Vector2(0.5f, 0.5f);
            deleteBoxRt.pivot     = new Vector2(0.5f, 0.5f);
            deleteBoxRt.sizeDelta = new Vector2(600, 320);
            deleteBoxGo.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(deleteBoxRt, deleteBoxRt.sizeDelta, thickness: 4f, color: Color.black);

            UIBuild.Text(deleteBoxGo.transform, "Heading", "削除しますか", yuGothic, 32, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -55), size: new Vector2(520, 50),
                alignment: TextAlignmentOptions.Center);

            TMP_Text deleteConfirmSongNameText = UIBuild.Text(deleteBoxGo.transform, "SongName", "", yuGothic, 24, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -130), size: new Vector2(520, 50),
                alignment: TextAlignmentOptions.Center);

            Button deleteYesButton = CreatePlainButton(deleteBoxGo.transform, "YesButton", "はい", yuGothic,
                pos: new Vector2(-130, -230), size: new Vector2(180, 60));
            Button deleteNoButton = CreatePlainButton(deleteBoxGo.transform, "NoButton", "いいえ", yuGothic,
                pos: new Vector2(130, -230), size: new Vector2(180, 60));

            deleteConfirmGo.SetActive(false);

            // SongSelectManager
            var managerGo = new GameObject("SongSelectManager", typeof(SongSelectManager));
            var manager = managerGo.GetComponent<SongSelectManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("listContent").objectReferenceValue     = contentRt;
            so.FindProperty("entryFont").objectReferenceValue       = agency;
            so.FindProperty("detailTitleText").objectReferenceValue = titleText;
            so.FindProperty("colorSwatch1").objectReferenceValue    = swatch1;
            so.FindProperty("colorSwatch2").objectReferenceValue    = swatch2;
            so.FindProperty("scoreText").objectReferenceValue       = scoreText;
            so.FindProperty("rankText").objectReferenceValue        = rankText;
            so.FindProperty("thumbnailVideoDisplay").objectReferenceValue = thumbnailVideoDisplay;
            so.FindProperty("deleteConfirmPanel").objectReferenceValue        = deleteConfirmGo;
            so.FindProperty("deleteConfirmSongNameText").objectReferenceValue = deleteConfirmSongNameText;
            so.ApplyModifiedProperties();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(playButton.onClick, manager.PlaySelected);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(deleteButton.onClick, manager.OnDeleteButtonClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(deleteYesButton.onClick, manager.ConfirmDelete);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(deleteNoButton.onClick, manager.CancelDelete);

            UIBuild.ApplyLetterbox(canvasGo);

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[SongSelectSceneSetup] 完了: {ScenePath} を作成しました");
        }

        // BracketButtonの角飾りは狭いポップアップ内だと収まりが悪いため、装飾のない枠ボタンを使う
        private static Button CreatePlainButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(rt, size, thickness: 3f, color: Color.black);
            UIBuild.Text(go.transform, "Label", label, font, 22, Color.black,
                pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: size,
                alignment: TextAlignmentOptions.Center);
            return go.GetComponent<Button>();
        }

        private static Image CreateSwatch(Transform parent, string name, Vector2 pos, bool filled)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(50, 50);

            var image = go.GetComponent<Image>();
            image.color = filled ? Color.white : new Color(0, 0, 0, 0);

            UIBuild.AddBorderFrame(rt, rt.sizeDelta, thickness: 3f, color: Color.black);

            if (!filled)
            {
                // 空き枠は×印を表示
                UIBuild.Text(go.transform, "X", "x", null, 24, new Color(0, 0, 0, 0.4f),
                    pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: new Vector2(50, 50),
                    alignment: TextAlignmentOptions.Center);
            }

            return image;
        }
    }
}
