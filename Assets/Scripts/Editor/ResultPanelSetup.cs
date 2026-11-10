using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Mophia.Core;
using Mophia.UI;

namespace Mophia.EditorTools
{
    /// <summary>
    /// 曲終了後に表示するリザルトパネル(スコア/ランク + 曲選択に戻るボタン)を、
    /// 現在開いているGamePlayシーンに追加する。ScoreUISetupと同じく既存シーンへの追加操作。
    /// </summary>
    public static class ResultPanelSetup
    {
        [MenuItem("Mophia/リザルトパネルを追加する")]
        public static void Setup()
        {
            GameObject gameManager = GameObject.Find("GameManager");
            if (gameManager == null)
            {
                Debug.LogError("[ResultPanelSetup] GameManager が見つかりません(シーンを開いていますか？)");
                return;
            }

            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[ResultPanelSetup] Canvas が見つかりません");
                return;
            }

            UIManager uiManager = gameManager.GetComponent<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("[ResultPanelSetup] UIManager が GameManager に見つかりません(先にスコア・コンボUIを追加してください)");
                return;
            }

            TMP_FontAsset agencyBold = UIBuild.GetOrCreateFontAsset("Assets/Fonts/AgencyFB-Bold SDF.asset", "Assets/Fonts/AGENCYB.TTF");
            TMP_FontAsset yuGothic   = UIBuild.GetOrCreateFontAsset("Assets/Fonts/YuGothic-Regular SDF.asset", "Assets/Fonts/YuGothR.ttc");
            UIBuild.SetFallback(agencyBold, yuGothic);

            Transform existingPanel = canvasGo.transform.Find("ResultPanel");
            if (existingPanel != null) Object.DestroyImmediate(existingPanel.gameObject);

            // 画面全体を覆う暗幕
            var panelGo = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // 中央の白いボックス
            var boxGo = new GameObject("ResultBox", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(panelGo.transform, false);
            var boxRt = (RectTransform)boxGo.transform;
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.pivot     = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(700, 560);
            boxGo.GetComponent<Image>().color = Color.white;
            UIBuild.AddBorderFrame(boxRt, boxRt.sizeDelta, thickness: 4f, color: Color.black);

            UIBuild.Text(boxGo.transform, "Heading", "RESULT", agencyBold, 56, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -50), size: new Vector2(600, 70),
                alignment: TextAlignmentOptions.Center);

            UIBuild.Text(boxGo.transform, "ScoreLabel", "score", yuGothic, 20, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -160), size: new Vector2(600, 30),
                alignment: TextAlignmentOptions.Center);

            TMP_Text resultScoreText = UIBuild.Text(boxGo.transform, "ResultScoreText", "0", agencyBold, 48, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -200), size: new Vector2(600, 60),
                alignment: TextAlignmentOptions.Center);

            UIBuild.Text(boxGo.transform, "RankLabel", "rank", yuGothic, 20, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -290), size: new Vector2(600, 30),
                alignment: TextAlignmentOptions.Center);

            TMP_Text resultRankText = UIBuild.Text(boxGo.transform, "ResultRankText", "-", agencyBold, 72, Color.black,
                pivot: new Vector2(0.5f, 1f), pos: new Vector2(0, -330), size: new Vector2(600, 90),
                alignment: TextAlignmentOptions.Center);

            Button backButton = UIBuild.BracketButton(boxGo.transform, "BackButton", "曲選択に戻る", yuGothic,
                pos: new Vector2(0, -215), size: new Vector2(420, 90));

            ResultPanel resultPanel = gameManager.GetComponent<ResultPanel>();
            if (resultPanel == null) resultPanel = gameManager.AddComponent<ResultPanel>();

            var panelSo = new SerializedObject(resultPanel);
            panelSo.FindProperty("panelRoot").objectReferenceValue = panelGo;
            panelSo.FindProperty("scoreText").objectReferenceValue = resultScoreText;
            panelSo.FindProperty("rankText").objectReferenceValue  = resultRankText;
            panelSo.ApplyModifiedProperties();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(backButton.onClick, resultPanel.BackToSongSelect);

            var uiManagerSo = new SerializedObject(uiManager);
            uiManagerSo.FindProperty("resultPanel").objectReferenceValue = resultPanel;
            uiManagerSo.ApplyModifiedProperties();

            panelGo.SetActive(false);

            EditorSceneManager.MarkSceneDirty(gameManager.scene);
            Debug.Log("[ResultPanelSetup] 完了: ResultPanelを追加し、UIManagerに配線しました。シーンを保存してください(Ctrl+S)");
        }
    }
}
