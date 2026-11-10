using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mophia.EditorTools
{
    /// <summary>
    /// GamePlayシーンにレターボックス(21.5インチ相当の中央縮小+黒帯)を適用する。
    /// 他の画面と違い、GamePlayはCanvas(ScreenSpaceOverlay: 動画・スコア・コンボ・リザルト)に加えて
    /// NotesCanvas(WorldSpace: ノーツ本体)があるため、Canvas側はUIBuild.ApplyLetterboxで、
    /// NotesCanvas側はMain CameraのViewport Rectを絞ることで同じ縮小率に揃える。
    /// 現在開いているGamePlayシーンに対して直接操作するので、実行前にシーンを開いておくこと。
    /// </summary>
    public static class GamePlayLetterboxSetup
    {
        [MenuItem("Mophia/GamePlay画面にレターボックスを適用する")]
        public static void Apply()
        {
            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[GamePlayLetterboxSetup] Canvas が見つかりません(GamePlayシーンを開いていますか？)");
                return;
            }

            GameObject cameraGo = GameObject.Find("Main Camera");
            if (cameraGo == null)
            {
                Debug.LogError("[GamePlayLetterboxSetup] Main Camera が見つかりません");
                return;
            }

            if (canvasGo.transform.Find("LetterboxRoot") != null)
            {
                Debug.LogWarning("[GamePlayLetterboxSetup] 既に適用済みのようです(LetterboxRootが存在)。再実行はスキップします");
                return;
            }

            float scale  = UIBuild.LetterboxScale;
            float margin = (1f - scale) / 2f;

            // 画面全体を黒でクリアする専用カメラ(Main Cameraの外側=黒帯になる部分の保険)
            var backdropGo = new GameObject("LetterboxBackdropCamera", typeof(Camera));
            var backdropCam = backdropGo.GetComponent<Camera>();
            backdropCam.clearFlags      = CameraClearFlags.SolidColor;
            backdropCam.backgroundColor = Color.black;
            backdropCam.cullingMask     = 0; // 何も描画しない、クリアするだけ
            backdropCam.depth           = -100;
            backdropCam.rect            = new Rect(0f, 0f, 1f, 1f);

            // Main Cameraは中央の縮小されたビューポートだけに描画するようにする
            // (動的な背景色・NotesCanvas(WorldSpace)のノーツも、これで一緒に縮小される)
            var mainCamera = cameraGo.GetComponent<Camera>();
            mainCamera.rect = new Rect(margin, margin, scale, scale);

            // Canvas(ScreenSpaceOverlay: 動画・スコア・コンボ・リザルト)側は他画面と同じ手法で縮小
            UIBuild.ApplyLetterbox(canvasGo, scale);

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Debug.Log("[GamePlayLetterboxSetup] 完了: GamePlay画面にレターボックスを適用しました。シーンを保存してください(Ctrl+S)");
        }
    }
}
