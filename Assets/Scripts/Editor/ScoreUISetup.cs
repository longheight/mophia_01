using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Mophia.Core;
using Mophia.UI;

namespace Mophia.EditorTools
{
    /// <summary>
    /// スコア/コンボ表示用のTextを作成し、UIManager/NotesRunnerの参照を配線する。
    /// 現在開いているGamePlayシーンに対して直接操作するので、実行前にシーンを開いておくこと。
    /// </summary>
    public static class ScoreUISetup
    {
        [MenuItem("Mophia/スコア・コンボUIを追加する")]
        public static void Setup()
        {
            GameObject gameManager = GameObject.Find("GameManager");
            if (gameManager == null)
            {
                Debug.LogError("[ScoreUISetup] GameManager が見つかりません(シーンを開いていますか？)");
                return;
            }

            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[ScoreUISetup] Canvas が見つかりません");
                return;
            }

            UIManager uiManager = gameManager.GetComponent<UIManager>();
            if (uiManager == null)
                uiManager = gameManager.AddComponent<UIManager>();

            Text scoreText = FindOrCreateText(canvas.transform, "ScoreText",
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchoredPos: new Vector2(-40, -30), size: new Vector2(400, 60),
                fontSize: 40, alignment: TextAnchor.UpperRight, initialText: "SCORE 000000");

            Text comboText = FindOrCreateText(canvas.transform, "ComboText",
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                anchoredPos: new Vector2(0, 200), size: new Vector2(400, 80),
                fontSize: 56, alignment: TextAnchor.MiddleCenter, initialText: "");

            var so = new SerializedObject(uiManager);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("comboText").objectReferenceValue = comboText;
            so.ApplyModifiedProperties();

            NotesRunner notesRunner = gameManager.GetComponent<NotesRunner>();
            if (notesRunner != null)
            {
                var runnerSo = new SerializedObject(notesRunner);
                runnerSo.FindProperty("uiManager").objectReferenceValue = uiManager;
                runnerSo.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[ScoreUISetup] NotesRunner が GameManager に見つからず、uiManager の配線をスキップしました");
            }

            EditorSceneManager.MarkSceneDirty(gameManager.scene);
            Debug.Log("[ScoreUISetup] 完了: ScoreText/ComboTextを作成し、UIManager/NotesRunnerに配線しました。シーンを保存してください(Ctrl+S)");
        }

        private static Text FindOrCreateText(Transform canvas, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor alignment, string initialText)
        {
            Transform existing = canvas.Find(name);
            if (existing != null)
            {
                // 既存分もアウトラインが無ければ追加する(白文字固定+白背景の曲だと
                // 文字が背景に溶けて見えなくなる問題の対策。再実行で既存シーンにも反映される)
                Text existingText = existing.GetComponent<Text>();
                EnsureOutline(existing.gameObject);
                return existingText;
            }

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(canvas, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = initialText;
            text.raycastTarget = false;

            EnsureOutline(go);

            return text;
        }

        // 白文字固定なので、曲(chart.jsonのbackgroundColor)が白系だと文字が背景に溶けて
        // 見えなくなる。背景色に関わらず視認できるよう黒アウトラインを付ける。
        private static void EnsureOutline(GameObject go)
        {
            if (go.GetComponent<Outline>() != null) return;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }
}
