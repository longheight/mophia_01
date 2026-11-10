using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mophia.Core;

namespace Mophia.EditorTools
{
    /// <summary>
    /// ノーツ専用の子Canvasを作り、NotesRunnerのnoteParentをそちらに向ける。
    /// 毎フレーム変化するノーツUIの再構築範囲を、動画表示やスコアUIから分離して
    /// Canvas全体のre-batchコストを減らす。
    /// 現在開いているGamePlayシーンに対して直接操作する。
    /// </summary>
    public static class NotesCanvasSetup
    {
        [MenuItem("Mophia/ノーツ専用Canvasに分離する")]
        public static void Setup()
        {
            GameObject mainCanvasGo = GameObject.Find("Canvas");
            if (mainCanvasGo == null)
            {
                Debug.LogError("[NotesCanvasSetup] Canvas が見つかりません(シーンを開いていますか？)");
                return;
            }

            Canvas mainCanvas = mainCanvasGo.GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogError("[NotesCanvasSetup] CanvasコンポーネントがCanvasオブジェクトに見つかりません");
                return;
            }

            Transform notesCanvasTf = mainCanvasGo.transform.Find("NotesCanvas");
            GameObject notesCanvasGo;
            if (notesCanvasTf == null)
            {
                notesCanvasGo = new GameObject("NotesCanvas", typeof(RectTransform), typeof(Canvas));
                notesCanvasGo.transform.SetParent(mainCanvasGo.transform, false);

                var rt = (RectTransform)notesCanvasGo.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                notesCanvasGo = notesCanvasTf.gameObject;
            }

            Canvas notesCanvas = notesCanvasGo.GetComponent<Canvas>();
            notesCanvas.renderMode = mainCanvas.renderMode;
            // 親と同じRender Modeでも、子に独自のCanvasコンポーネントを持たせるだけで
            // 再構築(バッチ)の範囲が分離される。描画順は親の後(手前)になるよう明示しておく。
            notesCanvas.overrideSorting = true;
            notesCanvas.sortingOrder = 1;

            GameObject gameManager = GameObject.Find("GameManager");
            NotesRunner notesRunner = gameManager != null ? gameManager.GetComponent<NotesRunner>() : null;
            if (notesRunner == null)
            {
                Debug.LogError("[NotesCanvasSetup] NotesRunner が見つかりません");
                return;
            }

            var so = new SerializedObject(notesRunner);
            so.FindProperty("noteParent").objectReferenceValue = notesCanvasGo.transform;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(mainCanvasGo.scene);
            Debug.Log("[NotesCanvasSetup] 完了: NotesCanvasを作成し、NotesRunner.noteParentを差し替えました。シーンを保存してください(Ctrl+S)");
        }
    }
}
