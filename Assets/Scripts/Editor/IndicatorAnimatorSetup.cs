using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Mophia.UI;

namespace Mophia.EditorTools
{
    /// <summary>
    /// 「目印」10preset分のAnimatorController/AnimationClipの骨組みを生成する。
    /// 生成される各presetの中身は空クリップなので、Animationウィンドウでデザインを作り込む。
    /// </summary>
    public static class IndicatorAnimatorSetup
    {
        // feature_decider.py の PRESETS_ROUND / PRESETS_SHARP と同じ命名(lineanimはアンダースコアなし)
        private static readonly string[] Presets =
        {
            "shrink_sharp",   "shrink_round",
            "lineanim_sharp", "lineanim_round",
            "spread_sharp",   "spread_round",
            "fill_sharp",     "fill_round",
            "vector_sharp",   "vector_round",
        };

        private const string ControllerPath = "Assets/Animations/IndicatorAnimator.controller";
        private const string ClipFolder      = "Assets/Animations/Clips";
        private const string PrefabPath      = "Assets/Prefabs/NotePrefab.prefab";

        [MenuItem("Mophia/目印アニメーションの骨組みを生成")]
        public static void Generate()
        {
            EnsureFolder("Assets/Animations");
            EnsureFolder(ClipFolder);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            int created = 0;
            foreach (string preset in Presets)
            {
                created += AddStateIfMissing(controller, stateMachine, preset + "_Approach");
                created += AddStateIfMissing(controller, stateMachine, preset + "_Onset");
            }

            // Write Defaults ONだと、クリップ内にカーブが無いプロパティ(Image.colorなど)を
            // 毎フレーム既定値に巻き戻してしまい、スクリプト側のApplyColorが上書きされてしまう。
            // そのため既存分も含め全ステートでOFFに統一する(可視状態は各クリップのActiveカーブで明示する)。
            // また、Onsetは自動再生(speed=1)に切り替えるため、Loop TimeがONのままだと
            // 短い長さのクリップが繰り返し再生されて二重に見えてしまう。あわせてOFFに統一する。
            int fixedWriteDefaults = 0;
            int fixedLoopTime = 0;
            foreach (var child in stateMachine.states)
            {
                if (child.state.writeDefaultValues)
                {
                    child.state.writeDefaultValues = false;
                    fixedWriteDefaults++;
                }

                if (child.state.motion is AnimationClip clip)
                {
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    if (settings.loopTime)
                    {
                        settings.loopTime = false;
                        AnimationUtility.SetAnimationClipSettings(clip, settings);
                        EditorUtility.SetDirty(clip);
                        fixedLoopTime++;
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            bool assigned = AssignToPrefab(controller);

            AssetDatabase.Refresh();
            Debug.Log($"[IndicatorAnimatorSetup] preset={Presets.Length} / 新規ステート={created} / " +
                      $"WriteDefaults OFFに修正={fixedWriteDefaults} / LoopTime OFFに修正={fixedLoopTime} / " +
                      $"controller={ControllerPath} / prefabへの割り当て={(assigned ? "OK" : "失敗、手動で割り当ててください")}");
        }

        private static int AddStateIfMissing(AnimatorController controller, AnimatorStateMachine sm, string stateName)
        {
            foreach (var child in sm.states)
                if (child.state.name == stateName)
                    return 0;

            string clipPath = $"{ClipFolder}/{stateName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = stateName };
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            AnimatorState state = sm.AddState(stateName);
            state.motion = clip;
            state.writeDefaultValues = false;
            return 1;
        }

        private static bool AssignToPrefab(AnimatorController controller)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                IndicatorController indicator = prefabRoot.GetComponentInChildren<IndicatorController>(true);
                if (indicator == null)
                {
                    Debug.LogWarning($"[IndicatorAnimatorSetup] {PrefabPath} にIndicatorControllerが見つかりません");
                    return false;
                }

                Animator animator = indicator.GetComponent<Animator>();
                if (animator == null)
                    animator = indicator.gameObject.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// spread以外の8preset(shrink/lineanim/fill/vector)は専用アニメーションがまだ無いため、
        /// 一時的にspreadと全く同じAnimationClip(実際に作り込まれている方)を再生させる。
        /// スプライトは既にspreadと同じ円/ひし形素材を割り当て済みなので、これで全preset共通で
        /// spreadの見た目・動きになる。専用アニメーションができたら、各presetの.animクリップに
        /// 個別のカーブを作り直してこのメソッドを実行し直せば不要になる。
        /// </summary>
        [MenuItem("Mophia/全プリセットを一時的にspreadと同じアニメーションにする")]
        public static void UseSpreadAnimationForAllPresets()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[IndicatorAnimatorSetup] {ControllerPath} が見つかりません(先に骨組み生成を実行してください)");
                return;
            }

            AnimationClip spreadRoundApproach = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/spread_round_Approach.anim");
            AnimationClip spreadRoundOnset    = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/spread_round_Onset.anim");
            AnimationClip spreadSharpApproach = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/spread_sharp_Approach.anim");
            AnimationClip spreadSharpOnset    = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/spread_sharp_Onset.anim");
            if (spreadRoundApproach == null || spreadRoundOnset == null || spreadSharpApproach == null || spreadSharpOnset == null)
            {
                Debug.LogError("[IndicatorAnimatorSetup] spreadのAnimationClipが見つかりません");
                return;
            }

            int changed = 0;
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                string name = child.state.name;
                if (name.StartsWith("spread_")) continue; // spread自身はそのまま

                AnimationClip target =
                    name.EndsWith("_round_Approach") ? spreadRoundApproach :
                    name.EndsWith("_round_Onset")    ? spreadRoundOnset    :
                    name.EndsWith("_sharp_Approach")  ? spreadSharpApproach :
                    name.EndsWith("_sharp_Onset")     ? spreadSharpOnset    : null;

                if (target == null || child.state.motion == target) continue;

                child.state.motion = target;
                changed++;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[IndicatorAnimatorSetup] 完了: spread以外の{changed}ステートを一時的にspreadのアニメーションに差し替えました");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
