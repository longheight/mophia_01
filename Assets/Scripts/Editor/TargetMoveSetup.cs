using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Mophia.UI;

namespace Mophia.EditorTools
{
    /// <summary>
    /// 「目印」を全preset共通のtarget(枠線)/move(充填)の2オブジェクトに統一するための
    /// 移行・生成スクリプト。
    ///
    /// 1. NotePrefabの中の旧オブジェクト(CircleImage/CircleFill/DiamondImage/DiamondFill/
    ///    GlowImage/ArrowImage/LineContainer)をTarget/Moveの2つに統合する
    /// 2. presetごとにtarget/moveのスプライトを登録する
    /// 3. Approach/OnsetクリップにMoveのscaleカーブを設定する(既存の古いカーブは削除)
    /// </summary>
    public static class TargetMoveSetup
    {
        private const string PrefabPath = "Assets/Prefabs/NotePrefab.prefab";
        private const string ClipFolder = "Assets/Animations/Clips";

        [MenuItem("Mophia/Target・Move構成に移行する")]
        public static void MigrateToTargetMove()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // 旧オブジェクトを流用してTarget/Moveにリネーム(位置・サイズはそのまま使う)
                Transform targetTf = prefabRoot.transform.Find("Target");
                if (targetTf == null)
                {
                    Transform circleImageTf = prefabRoot.transform.Find("CircleImage");
                    if (circleImageTf != null)
                    {
                        circleImageTf.name = "Target";
                        targetTf = circleImageTf;
                    }
                }

                Transform moveTf = prefabRoot.transform.Find("Move");
                if (moveTf == null)
                {
                    Transform circleFillTf = prefabRoot.transform.Find("CircleFill");
                    if (circleFillTf != null)
                    {
                        circleFillTf.name = "Move";
                        moveTf = circleFillTf;
                    }
                }

                if (targetTf == null || moveTf == null)
                {
                    Debug.LogError("[TargetMoveSetup] Target/Moveの元になるオブジェクトが見つかりません");
                    return;
                }

                // 使わなくなった旧オブジェクトを削除
                foreach (var name in new[] { "DiamondImage", "DiamondFill", "GlowImage", "ArrowImage", "LineContainer" })
                {
                    Transform old = prefabRoot.transform.Find(name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }

                Image targetImg = targetTf.GetComponent<Image>();
                Image moveImg   = moveTf.GetComponent<Image>();
                moveImg.transform.localScale = Vector3.zero;

                IndicatorController indicator = prefabRoot.GetComponent<IndicatorController>();
                if (indicator == null)
                {
                    Debug.LogError("[TargetMoveSetup] IndicatorController が見つかりません");
                    return;
                }

                var so = new SerializedObject(indicator);
                so.FindProperty("target").objectReferenceValue = targetImg;
                so.FindProperty("move").objectReferenceValue   = moveImg;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            RegisterPresetSprites("spread_round",
                "Assets/SampleImages/circle_outline.png", "Assets/SampleImages/circle_fill.png");
            RegisterPresetSprites("spread_sharp",
                "Assets/SampleImages/diamond_outline.png", "Assets/SampleImages/diamond_fill.png");

            SetupGrowClip("spread_round_Approach", true);
            SetupGrowClip("spread_round_Onset", false);
            SetupGrowClip("spread_sharp_Approach", true);
            SetupGrowClip("spread_sharp_Onset", false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TargetMoveSetup] 完了: Target/Move構成に移行しました");
        }

        /// <summary>
        /// shrink/fill/lineanim/vectorはまだ専用の見た目素材が無いため、暫定でspreadと同じ
        /// 円(round)/ひし形(sharp)素材を使い回して登録する(ノーツが見えない状態を解消する応急処置)。
        /// 専用素材ができたらRegisterPresetSpritesを個別に呼び直せば上書きされる。
        /// </summary>
        [MenuItem("Mophia/未設定プリセットに仮スプライトを割り当てる")]
        public static void AssignPlaceholderSpritesForRemainingPresets()
        {
            const string circleOutline = "Assets/SampleImages/circle_outline.png";
            const string circleFill    = "Assets/SampleImages/circle_fill.png";
            const string diamondOutline = "Assets/SampleImages/diamond_outline.png";
            const string diamondFill    = "Assets/SampleImages/diamond_fill.png";

            foreach (string basePreset in new[] { "shrink", "fill", "lineanim", "vector" })
            {
                RegisterPresetSprites($"{basePreset}_round", circleOutline, circleFill);
                RegisterPresetSprites($"{basePreset}_sharp", diamondOutline, diamondFill);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TargetMoveSetup] 完了: 未設定だった8プリセットに仮スプライト(円/ひし形)を割り当てました");
        }

        /// <summary>
        /// preset用に新しくtarget/moveスプライトを登録したいときに使う汎用メソッド。
        /// メニューから直接は呼べないので、専用のMenuItemを追加するか、
        /// このメソッドをコピーして新しいpreset名・スプライトパスを渡す。
        /// </summary>
        public static void RegisterPresetSprites(string preset, string targetSpritePath, string moveSpritePath)
        {
            Sprite targetSprite = ImportAsSprite(targetSpritePath);
            Sprite moveSprite   = ImportAsSprite(moveSpritePath);
            if (targetSprite == null || moveSprite == null)
            {
                Debug.LogError($"[TargetMoveSetup] {preset}: スプライトが見つかりません ({targetSpritePath} / {moveSpritePath})");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                IndicatorController indicator = prefabRoot.GetComponent<IndicatorController>();
                if (indicator == null)
                {
                    Debug.LogError("[TargetMoveSetup] IndicatorController が見つかりません");
                    return;
                }

                var so = new SerializedObject(indicator);
                var arr = so.FindProperty("presetSprites");

                int index = -1;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("preset").stringValue == preset)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    index = arr.arraySize;
                    arr.InsertArrayElementAtIndex(index);
                }

                var elem = arr.GetArrayElementAtIndex(index);
                elem.FindPropertyRelative("preset").stringValue = preset;
                elem.FindPropertyRelative("targetSprite").objectReferenceValue = targetSprite;
                elem.FindPropertyRelative("moveSprite").objectReferenceValue   = moveSprite;

                so.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Moveのscaleを0→overallScale(Approach)またはoverallScale固定(Onset)にする汎用クリップ設定。
        /// Target側もoverallScaleで固定する(overallScale=1のときは今までと同じ見た目)。
        /// 古いバージョンで焼き込まれた別名(CircleFill/DiamondFill等)のカーブは全部削除する。
        /// </summary>
        public static void SetupGrowClip(string stateName, bool isApproach, float overallScale = 1f)
        {
            string path = $"{ClipFolder}/{stateName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                Debug.LogError($"[TargetMoveSetup] {path} が見つかりません(先にAnimatorの骨組み生成を実行してください)");
                return;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            // イージング(EaseInOut)だとonset直前で減速し、実際のonsetより早く「広がり切った」ように
            // 見えて判定が遅れて感じる原因になる(fill/shrinkと同じ問題)。尺は変えずLinearにする。
            AnimationCurve moveCurve = isApproach
                ? AnimationCurve.Linear(0f, 0f, 1f, overallScale)
                : AnimationCurve.Constant(0f, 1f, overallScale);
            // Targetは元々カーブが無く常に等倍(1.0)だったが、それだと全体スケールを
            // overallScaleで縮めてもTargetだけ縮まず不自然になるため、TargetもoverallScaleで固定する。
            AnimationCurve targetCurve = AnimationCurve.Constant(0f, 1f, overallScale);

            foreach (var axis in new[] { "x", "y", "z" })
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Move", typeof(Transform), $"m_LocalScale.{axis}"), moveCurve);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Target", typeof(Transform), $"m_LocalScale.{axis}"), targetCurve);
            }

            EditorUtility.SetDirty(clip);
        }

        /// <summary>
        /// spread_round/spread_sharpのApproach/Onsetクリップを(Linearカーブで)再生成し直す。
        /// MigrateToTargetMove内でも同じ処理を行っているが、それは初回移行専用の大掛かりな処理
        /// (旧オブジェクトのリネーム/削除やスプライト登録も含む)なので、カーブだけ直したい時のために
        /// 同じSetupGrowClip呼び出しだけを単独で再実行できるメニューを用意する。
        /// </summary>
        [MenuItem("Mophia/spreadプリセットの専用アニメーションを設定する")]
        public static void SetupSpreadPresets()
        {
            const float overallScale = 0.6f; // 全体的にもう少し小さく
            SetupGrowClip("spread_round_Approach", true, overallScale);
            SetupGrowClip("spread_round_Onset", false, overallScale);
            SetupGrowClip("spread_sharp_Approach", true, overallScale);
            SetupGrowClip("spread_sharp_Onset", false, overallScale);

            AssetDatabase.SaveAssets();
            Debug.Log("[TargetMoveSetup] 完了: spread_round/spread_sharpのカーブをLinearで再生成しました");
        }

        private const string ControllerPath = "Assets/Animations/IndicatorAnimator.controller";

        /// <summary>
        /// shrink_round/shrink_sharpに専用の「外側(target)は固定、内側(move)が判定タイミングに
        /// 向けて縮んでいく」アニメーションを設定する。スプライトはspreadと同じ円/ひし形を使う
        /// (仮スプライト割り当て時に既に登録済み)ため、ここではAnimatorControllerの参照先を
        /// 専用クリップ(shrink_round_Approach.anim等)へ戻し、その中身をshrink用カーブで上書きする。
        /// </summary>
        [MenuItem("Mophia/shrinkプリセットの専用アニメーションを設定する")]
        public static void SetupShrinkPresets() => ApplyShrinkStyleAnimation("shrink");

        /// <summary>
        /// fill_round/fill_sharpに専用の「外枠(target)は固定表示、内側(move)がパイチャートのように
        /// 塗りつぶし量が増えていく」アニメーションを設定する。moveのImageをType.Filled(Radial360)に
        /// 切り替えるのはIndicatorController.ApplySprites側(preset名が"fill"始まりかで判定)で行う。
        /// </summary>
        [MenuItem("Mophia/fillプリセットの専用アニメーションを設定する")]
        public static void SetupFillPresets()
        {
            const float overallScale = 0.75f; // 全体的にもう少し小さく(spreadと同じ比率)
            ApplyPresetAnimation("fill", (stateName, isApproach) => SetupFillClip(stateName, isApproach, overallScale));
        }

        private static void ApplyShrinkStyleAnimation(string basePreset)
        {
            const float startScale    = 1.6f; // 初期状態の枠線を大きくして、縮む距離自体を増やし体感速度を上げる
            const float shrinkToScale = 0.4f;
            ApplyPresetAnimation(basePreset, (stateName, isApproach) => SetupShrinkClip(stateName, isApproach, startScale, shrinkToScale));
        }

        /// <summary>
        /// {basePreset}_round/{basePreset}_sharpの4ステートについて、AnimatorControllerの参照先を
        /// (spreadと共用していた状態から)専用クリップへ戻し、setupClipで中身を書き込む共通処理。
        /// </summary>
        private static void ApplyPresetAnimation(string basePreset, System.Action<string, bool> setupClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[TargetMoveSetup] {ControllerPath} が見つかりません(先にAnimatorの骨組み生成を実行してください)");
                return;
            }

            foreach (string stateName in new[]
            {
                $"{basePreset}_round_Approach", $"{basePreset}_round_Onset",
                $"{basePreset}_sharp_Approach", $"{basePreset}_sharp_Onset",
            })
            {
                bool isApproach = stateName.EndsWith("_Approach");
                setupClip(stateName, isApproach);

                string clipPath = $"{ClipFolder}/{stateName}.anim";
                AnimationClip ownClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (ownClip == null)
                {
                    Debug.LogError($"[TargetMoveSetup] {clipPath} を読み込めませんでした");
                    continue;
                }

                bool found = false;
                foreach (var child in controller.layers[0].stateMachine.states)
                {
                    if (child.state.name != stateName) continue;
                    found = true;

                    AnimationClip before = child.state.motion as AnimationClip;
                    child.state.motion = ownClip; // UseSpreadAnimationForAllPresetsで書き換えられた分を元に戻す
                    EditorUtility.SetDirty(child.state);

                    Debug.Log($"[TargetMoveSetup] {stateName}: motionを {(before != null ? before.name : "null")} → {ownClip.name} に変更");
                }
                if (!found)
                    Debug.LogError($"[TargetMoveSetup] AnimatorControllerに{stateName}ステートが見つかりません");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TargetMoveSetup] 完了: {basePreset}_round/{basePreset}_sharpに専用のアニメーションを設定しました");
        }

        /// <summary>
        /// Move(内側)のFillAmountを0→1(Approach)または1固定(Onset)にする(パイチャート状の塗りつぶし)。
        /// HideAll()でmoveのscaleは0に戻されるため、可視化のためscale自体は常に等倍固定にしておく
        /// (実際の見た目の変化はscaleではなくFillAmountが担う)。Target(外枠)は常に等倍固定表示。
        /// Type.Filled/Radial360への切り替えはIndicatorController.ApplySprites側で行う。
        /// </summary>
        private static void SetupFillClip(string stateName, bool isApproach, float overallScale)
        {
            string path = $"{ClipFolder}/{stateName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                Debug.LogError($"[TargetMoveSetup] {path} が見つかりません(先にAnimatorの骨組み生成を実行してください)");
                return;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            // イージングをかけず、判定タイミングまで一定速度で塗りつぶす
            AnimationCurve fillCurve = isApproach
                ? AnimationCurve.Linear(0f, 0f, 1f, 1f)
                : AnimationCurve.Constant(0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Move", typeof(Image), "m_FillAmount"), fillCurve);

            // Target/Moveとも常にoverallScaleで固定(全体サイズ)。Targetは元々カーブが無く
            // 常に等倍だったが、それだと全体スケールを縮めてもTargetだけ縮まず不自然になる。
            AnimationCurve constantScale = AnimationCurve.Constant(0f, 1f, overallScale);
            foreach (var axis in new[] { "x", "y", "z" })
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Move", typeof(Transform), $"m_LocalScale.{axis}"), constantScale);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Target", typeof(Transform), $"m_LocalScale.{axis}"), constantScale);
            }

            EditorUtility.SetDirty(clip);
        }

        /// <summary>
        /// Target(外枠)のscaleを startScale→shrinkToScale(Approach)またはshrinkToScale固定(Onset)にする。
        /// Move(内側の点)は常に等倍固定で表示する(参考GIFの通り、縮むのは外枠側)。
        /// (9-sliceで枠線の太さを一定に保おうとしたが、円/ひし形のような曲線形状には合わず
        /// 大きく歪んだため撤回。localScaleでの均等縮小に戻す)
        /// </summary>
        private static void SetupShrinkClip(string stateName, bool isApproach, float startScale, float shrinkToScale)
        {
            string path = $"{ClipFolder}/{stateName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                Debug.LogError($"[TargetMoveSetup] {path} が見つかりません(先にAnimatorの骨組み生成を実行してください)");
                return;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            // TargetはstartScaleからshrinkToScaleまで縮む。Moveはその「縮んだ後のTarget」と同じ
            // 大きさで固定する(Moveを等倍のままにすると、Targetより常に大きい/同じになり、
            // 描画順で手前のMoveがTargetを覆い隠して見えなくなってしまう)
            // イージング(EaseInOut)だとonset直前で減速し、実際のonsetより早く「縮み切った」ように
            // 見えて判定が遅れて感じる原因になる(fillで一度直したのと同じ問題)。尺は変えずLinearにする。
            AnimationCurve targetCurve = isApproach
                ? AnimationCurve.Linear(0f, startScale, 1f, shrinkToScale)
                : AnimationCurve.Constant(0f, 1f, shrinkToScale);
            AnimationCurve moveCurve = AnimationCurve.Constant(0f, 1f, shrinkToScale);

            foreach (var axis in new[] { "x", "y", "z" })
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Target", typeof(Transform), $"m_LocalScale.{axis}"), targetCurve);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve("Move", typeof(Transform), $"m_LocalScale.{axis}"), moveCurve);
            }

            EditorUtility.SetDirty(clip);
        }

        private static Sprite ImportAsSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
