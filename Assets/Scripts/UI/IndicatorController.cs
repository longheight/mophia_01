using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mophia.Data;

namespace Mophia.UI
{
    /// <summary>
    /// 「目印」は全preset共通で target(枠線) / move(充填) の2オブジェクトだけで表現する。
    /// preset ごとに違うのはスプライト(見た目)とAnimatorのモーションだけで、
    /// オブジェクト自体は増減しない(=別の形が混ざって見える事故が構造的に起きない)。
    ///
    /// 色は固定で target=secondary, move=primary。
    ///
    /// Animatorステートは preset文字列に対応する
    ///   "<preset>_Approach" (接近中。0〜1を本スクリプトが強制的にスクラブする)
    ///   "<preset>_Onset"    (判定タイミング到達後。等速の実時間再生)
    /// を使う。中身(move の scale など)はUnityのAnimationウィンドウで自由に作り込む。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class IndicatorController : MonoBehaviour
    {
        [Serializable]
        private struct PresetSprites
        {
            public string preset;
            public Sprite targetSprite; // 枠線
            public Sprite moveSprite;   // 充填/動く方
        }

        [Header("表示対象(全preset共通でこの2つだけ)")]
        [SerializeField] private Image target;
        [SerializeField] private Image move;

        [Header("preset単位のスプライト差し替え")]
        [SerializeField] private PresetSprites[] presetSprites;

        [Header("Editor確認用(Play Mode不要でPlayを試せる)")]
        [SerializeField] private string previewPreset = "spread_round";
        [SerializeField] private Color previewPrimary = Color.white;
        [SerializeField] private Color previewSecondary = Color.white;

        private const int Layer = 0;
        private const float DismissDuration = 0.15f;
        private const float DismissScaleBoost = 0.4f; // 掛け算ではなく足し算(shrinkのように基準スケールが小さいpresetでも変化量が一定になるように)

        private Animator _animator;
        private Coroutine _routine;

        [ContextMenu("プレビュー適用(previewPreset/Primary/Secondaryを反映)")]
        private void PreviewApplyColor()
        {
            var testColor = new ColorData
            {
                primary   = new[] { previewPrimary.r,   previewPrimary.g,   previewPrimary.b,   previewPrimary.a },
                secondary = new[] { previewSecondary.r, previewSecondary.g, previewSecondary.b, previewSecondary.a },
                isGradient = true,
                type = "gradient",
            };
            ApplySprites(previewPreset);
            ApplyColor(testColor);
            Debug.Log($"[IndicatorController] プレビュー適用: preset={previewPreset}");
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        // ゲーム開始前(音声再生が始まる前)に、実際に使うpresetのAnimatorステート/Spriteを
        // 一度だけ触っておく。初回使用時に発生する初期化コストを、負荷に敏感なタイミングより
        // 前に前倒しして済ませておくためのもの。
        public void WarmUp(string preset)
        {
            ApplySprites(preset);

            _animator.Rebind();
            _animator.Update(0f);

            int hash = Animator.StringToHash(preset + "_Approach");
            if (_animator.HasState(Layer, hash))
            {
                _animator.Play(hash, Layer, 0f);
                _animator.Update(0f);
            }

            HideAll();
        }

        public void Play(string preset, ColorData color, float timeUntilOnset, float direction, float duration)
        {
            Stop();
            ApplySprites(preset);
            ApplyColor(color);
            ApplyDirection(preset, direction);

            _routine = StartCoroutine(PlayRoutine(preset, timeUntilOnset, duration));
        }

        private void ApplySprites(string preset)
        {
            PresetSprites sprites = FindSprites(preset);
            if (target != null) target.sprite = sprites.targetSprite;
            if (move   != null) move.sprite   = sprites.moveSprite;

            // fillプリセットだけ、moveをパイチャート状に塗りつぶすType.Filled(Radial360)にする。
            // 他のpresetはtarget/moveを共用する通常のType.Simple(スケールで動かす)に戻す。
            if (move != null)
            {
                if (preset.StartsWith("fill"))
                {
                    move.type          = Image.Type.Filled;
                    move.fillMethod    = Image.FillMethod.Radial360;
                    move.fillOrigin    = (int)Image.Origin360.Top;
                    move.fillClockwise = true;
                    move.fillAmount    = 0f;
                }
                else
                {
                    move.type = Image.Type.Simple;
                }
            }
        }

        private PresetSprites FindSprites(string preset)
        {
            foreach (var s in presetSprites)
                if (s.preset == preset)
                    return s;

            Debug.LogWarning($"[IndicatorController] presetSprites未設定: {preset}");
            return new PresetSprites { preset = preset };
        }

        private void ApplyColor(ColorData color)
        {
            if (color == null || color.type == "none") return;

            if (target != null) target.color = WithAlpha(color.SecondaryColor, target.color.a);
            if (move   != null) move.color   = WithAlpha(color.PrimaryColor,   move.color.a);
        }

        private void ApplyDirection(string preset, float direction)
        {
            if (!preset.StartsWith("vector")) return;

            // direction: 0=上, 時計回り → Unityの回転(Z軸)に変換(Z回転は反時計回りが正なので符号反転)
            var rot = Quaternion.Euler(0f, 0f, -direction);
            if (target != null) target.rectTransform.localRotation = rot;
            if (move   != null) move.rectTransform.localRotation   = rot;
        }

        public void Stop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _animator.speed = 1f;
            HideAll();
        }

        // 判定された瞬間に呼ぶ。即座に隠すのではなく、一瞬スケールアップ+フェードアウトしてから隠す
        public void Dismiss()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _animator.speed = 1f;
            _animator.enabled = false;
            StartCoroutine(DismissRoutine());
        }

        private IEnumerator DismissRoutine()
        {
            // 弾ける演出は「今実際に見えているスケール」を基準にする。一律1.0から始めると、
            // shrink(判定時0.4)のようにその時点で既に縮んでいるpresetでは、押した瞬間に
            // 1.0へスナップしてから弾けるように見えてしまい、押した反応として繋がらない。
            float targetBaseScale = target != null ? target.transform.localScale.x : 1f;
            float moveBaseScale   = move   != null ? move.transform.localScale.x   : 1f;

            float elapsed = 0f;
            while (elapsed < DismissDuration)
            {
                float t = elapsed / DismissDuration;
                // 線形だと最初の数フレームでほぼ変化が無く「押した瞬間なのに反応が遅れて見える」ため、
                // イーズアウト(最初に大きく動いて後半で減速)にして押した瞬間の反応を強くする
                float eased = 1f - (1f - t) * (1f - t);
                float alpha = Mathf.Lerp(1f, 0f, eased);

                ApplyDismissFrame(target, Mathf.Lerp(targetBaseScale, targetBaseScale + DismissScaleBoost, eased), alpha);
                ApplyDismissFrame(move,   Mathf.Lerp(moveBaseScale,   moveBaseScale   + DismissScaleBoost, eased), alpha);

                elapsed += Time.deltaTime;
                yield return null;
            }

            HideAll();
            ResetTransformsAndAlpha();
        }

        private static void ApplyDismissFrame(Image img, float scale, float alpha)
        {
            if (img == null) return;
            img.transform.localScale = Vector3.one * scale;
            img.color = WithAlpha(img.color, alpha);
        }

        // 次回Playで使い回せるよう、scale/alphaを既定値に戻す(表示ON/OFF自体はHideAllが担当)
        private void ResetTransformsAndAlpha()
        {
            if (target != null) { target.transform.localScale = Vector3.one; target.color = WithAlpha(target.color, 1f); }
            if (move   != null) { move.color = WithAlpha(move.color, 1f); } // scaleはHideAllで0に戻す
        }

        private IEnumerator PlayRoutine(string preset, float timeUntilOnset, float duration)
        {
            // SetActive(true)と同じフレームだとAnimatorの内部グラフ構築が
            // 間に合っておらずHasStateが誤って失敗することがあるため1フレーム待つ
            yield return null;

            // Instantiate直後にSetActive(false)されるプール運用のせいで、Animatorの
            // コントローラーバインドが中断されたまま復帰することがあるため強制的に再バインドする。
            _animator.Rebind();
            _animator.Update(0f);

            if (target != null) target.gameObject.SetActive(true);

            int approachHash = Animator.StringToHash(preset + "_Approach");
            int onsetHash    = Animator.StringToHash(preset + "_Onset");

            if (!_animator.HasState(Layer, approachHash))
            {
                Debug.LogWarning($"[IndicatorController] Animatorステート未定義: {preset}_Approach");
                yield break;
            }

            // Approach: speed=0で自動再生を止め、接近時間ぴったりにnormalizedTimeを手動スクラブする
            _animator.enabled = true;
            _animator.speed   = 0f;

            // 毎フレームUpdate(0f)まで呼ぶと、接近中のノーツが複数重なったときにAnimator評価が
            // 二重(手動+Unity自身の自動評価分)になり負荷が増える。Play()だけ呼べば、
            // enabled=trueな限りUnityの自動評価が次のタイミングで反映してくれる(1フレーム遅れる程度)。
            float elapsed = 0f;
            while (elapsed < timeUntilOnset)
            {
                float t = timeUntilOnset > 0f ? Mathf.Clamp01(elapsed / timeUntilOnset) : 1f;
                _animator.Play(approachHash, Layer, t);
                yield return null;
                elapsed += Time.deltaTime;
            }
            _animator.Play(approachHash, Layer, 1f);
            _animator.Update(0f);

            // longノーツ: onset到達後もduration分だけ保持(Approachの最終フレームのまま)
            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            // Onset: ここからは通常の自動再生に切り替える
            if (_animator.HasState(Layer, onsetHash))
            {
                _animator.speed = 1f;
                _animator.Play(onsetHash, Layer, 0f);
            }
        }

        private void HideAll()
        {
            if (target != null) target.gameObject.SetActive(false);
            if (move   != null) move.transform.localScale = Vector3.zero;
        }

        private static Color WithAlpha(Color c, float a) =>
            new Color(c.r, c.g, c.b, a);
    }
}
