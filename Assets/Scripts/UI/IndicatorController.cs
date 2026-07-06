using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Mophia.Data;

namespace Mophia.UI
{
    /// <summary>
    /// preset文字列に対応したインジケーターUIアニメーションを制御する。
    /// DOTweenを使用。各presetはCoroutine+DOTweenの組み合わせで実装。
    /// </summary>
    public class IndicatorController : MonoBehaviour
    {
        [Header("共通")]
        [SerializeField] private Image circleImage;       // shrink_circle / spread / fill_circle用
        [SerializeField] private Image[] lineImages;      // line_anim用 (4本想定)
        [SerializeField] private Image arrowImage;        // vector_note用
        [SerializeField] private Image glowImage;         // round系のぼかし用オーバーレイ

        // sharp/round共通: roundはglowImageのalphaを上げて縁をぼかす
        private const float RoundGlowAlpha = 0.6f;
        private const float SharpGlowAlpha = 0.0f;

        private Sequence _sequence;
        private Coroutine _routine;

        // -------------------------------------------------------
        // 公開API
        // -------------------------------------------------------

        public void Play(string preset, ColorData color, float timeUntilOnset, float direction, float duration)
        {
            Debug.Log($"[IndicatorController] Play called: preset={preset} timeUntilOnset={timeUntilOnset:F2}");
            Stop();
            ApplyColor(color);

            bool isRound = preset.EndsWith("_round");
            if (glowImage != null)
                glowImage.color = WithAlpha(glowImage.color, isRound ? RoundGlowAlpha : SharpGlowAlpha);

            switch (preset)
            {
                case "shrink_circle_sharp":
                case "shrink_circle_round":
                    _routine = StartCoroutine(ShrinkCircle(timeUntilOnset, isRound));
                    break;

                case "line_anim_sharp":
                case "line_anim_round":
                    _routine = StartCoroutine(LineAnim(timeUntilOnset, isRound));
                    break;

                case "spread_sharp":
                case "spread_round":
                    _routine = StartCoroutine(Spread(timeUntilOnset, isRound));
                    break;

                case "fill_circle_sharp":
                case "fill_circle_round":
                    _routine = StartCoroutine(FillCircle(timeUntilOnset, duration, isRound));
                    break;

                case "vector_note_sharp":
                case "vector_note_round":
                    _routine = StartCoroutine(VectorNote(timeUntilOnset, direction, isRound));
                    break;

                default:
                    Debug.LogWarning($"[IndicatorController] 未実装のpreset: {preset}");
                    break;
            }
        }

        public void Stop()
        {
            _sequence?.Kill();
            if (_routine != null) StopCoroutine(_routine);
            ResetAll();
        }

        // -------------------------------------------------------
        // 色の適用
        // -------------------------------------------------------

        private void ApplyColor(ColorData color)
        {
            if (color == null || color.type == "none") return;

            Color primary = ParseHex(color.primary, Color.white);

            if (circleImage != null) circleImage.color = primary;
            if (arrowImage != null) arrowImage.color = primary;
            foreach (var line in lineImages)
                if (line != null) line.color = primary;

            // gradient: glowImageにsecondaryを載せる
            if (color.type == "gradient" && !string.IsNullOrEmpty(color.secondary))
            {
                Color secondary = ParseHex(color.secondary, primary);
                if (glowImage != null) glowImage.color = WithAlpha(secondary, glowImage.color.a);
            }
        }

        // -------------------------------------------------------
        // preset実装
        // -------------------------------------------------------

        /// <summary>
        /// shrink_circle: 大きい円(scale=3)がonsetに向かって1.0に縮む
        /// roundはglowで縁をぼかす
        /// </summary>
        private IEnumerator ShrinkCircle(float timeUntilOnset, bool isRound)
        {
            if (circleImage == null) yield break;

            circleImage.gameObject.SetActive(true);
            circleImage.transform.localScale = Vector3.one * 3f;
            circleImage.color = WithAlpha(circleImage.color, 1f);

            _sequence = DOTween.Sequence();
            _sequence.Append(
                circleImage.transform
                    .DOScale(1f, timeUntilOnset)
                    .SetEase(Ease.InQuart)
            );
            // roundはフェードインしながら縮む（ふわっとした印象）
            if (isRound)
            {
                _sequence.Join(
                    circleImage.DOFade(0.85f, timeUntilOnset * 0.5f)
                        .SetEase(Ease.OutQuad)
                );
            }

            yield return _sequence.WaitForCompletion();

            // onset到達: パルス
            circleImage.transform
                .DOPunchScale(Vector3.one * 0.15f, 0.1f, 1)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// line_anim: 4方向のラインが外から中心へ収束
        /// sharpは直線的、roundは弧を描くように回転しながら来る
        /// </summary>
        private IEnumerator LineAnim(float timeUntilOnset, bool isRound)
        {
            if (lineImages == null || lineImages.Length == 0) yield break;

            float startDist = 300f;
            float duration = timeUntilOnset;

            _sequence = DOTween.Sequence();

            for (int i = 0; i < lineImages.Length; i++)
            {
                if (lineImages[i] == null) continue;

                float angle = 90f * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 startPos = new Vector3(
                    Mathf.Cos(rad) * startDist,
                    Mathf.Sin(rad) * startDist,
                    0f
                );

                lineImages[i].gameObject.SetActive(true);
                lineImages[i].rectTransform.anchoredPosition = startPos;
                lineImages[i].color = WithAlpha(lineImages[i].color, 0f);

                // フェードイン
                _sequence.Join(
                    lineImages[i].DOFade(1f, duration * 0.3f)
                        .SetEase(Ease.OutQuad)
                );
                // 中心へ移動
                Ease moveEase = isRound ? Ease.InOutSine : Ease.InQuart;
                _sequence.Join(
                    lineImages[i].rectTransform
                        .DOAnchorPos(Vector2.zero, duration)
                        .SetEase(moveEase)
                );
                // roundは回転も加える
                if (isRound)
                {
                    lineImages[i].rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                    _sequence.Join(
                        lineImages[i].rectTransform
                            .DOLocalRotate(new Vector3(0, 0, angle + 45f), duration)
                            .SetEase(Ease.InOutSine)
                    );
                }
            }

            yield return _sequence.WaitForCompletion();
        }

        /// <summary>
        /// spread: onset時に中心からリングが外に拡散してフェードアウト
        /// timeUntilOnsetの間は点滅で待機、onset到達で拡散
        /// </summary>
        private IEnumerator Spread(float timeUntilOnset, bool isRound)
        {
            if (circleImage == null) yield break;

            circleImage.gameObject.SetActive(true);
            circleImage.transform.localScale = Vector3.one * 0.8f;

            // onset前: ゆっくり点滅（存在感を示す）
            float blinkDuration = timeUntilOnset;
            float blinkCycle = 0.6f;
            _sequence = DOTween.Sequence();
            _sequence.Append(
                circleImage.DOFade(0.4f, blinkCycle * 0.5f).SetEase(Ease.InOutSine)
            );
            _sequence.Append(
                circleImage.DOFade(1.0f, blinkCycle * 0.5f).SetEase(Ease.InOutSine)
            );
            _sequence.SetLoops(Mathf.CeilToInt(blinkDuration / blinkCycle));
            yield return new WaitForSeconds(blinkDuration);
            _sequence.Kill();

            // onset: 外に拡散
            circleImage.transform.localScale = Vector3.one;
            circleImage.color = WithAlpha(circleImage.color, 1f);

            float spreadTime = isRound ? 0.4f : 0.25f;
            _sequence = DOTween.Sequence();
            _sequence.Append(
                circleImage.transform
                    .DOScale(isRound ? 3.5f : 3.0f, spreadTime)
                    .SetEase(isRound ? Ease.OutSine : Ease.OutExpo)
            );
            _sequence.Join(
                circleImage.DOFade(0f, spreadTime).SetEase(Ease.OutQuad)
            );
            yield return _sequence.WaitForCompletion();
        }

        /// <summary>
        /// fill_circle: 円をImage.fillAmountで時計回りに充填、long noteのduration分かけて満たす
        /// onset到達で充填完了→解放エフェクト
        /// </summary>
        private IEnumerator FillCircle(float timeUntilOnset, float duration, bool isRound)
        {
            if (circleImage == null) yield break;

            // fillAmountを使うのでImage TypeをFilledに設定
            circleImage.type = Image.Type.Filled;
            circleImage.fillMethod = Image.FillMethod.Radial360;
            circleImage.fillOrigin = (int)Image.Origin360.Top;
            circleImage.fillClockwise = true;
            circleImage.fillAmount = 0f;
            circleImage.gameObject.SetActive(true);

            // onset前: fillAmountを0→1に（timeUntilOnset秒かけて）
            _sequence = DOTween.Sequence();
            _sequence.Append(
                DOTween.To(
                    () => circleImage.fillAmount,
                    v => circleImage.fillAmount = v,
                    1f,
                    timeUntilOnset
                ).SetEase(Ease.Linear)
            );
            yield return _sequence.WaitForCompletion();

            // long noteのduration分: 充填状態を維持
            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            // 解放: スケールアップ＆フェードアウト
            float releaseTime = isRound ? 0.35f : 0.2f;
            _sequence = DOTween.Sequence();
            _sequence.Append(
                circleImage.transform
                    .DOScale(isRound ? 1.8f : 1.5f, releaseTime)
                    .SetEase(Ease.OutExpo)
            );
            _sequence.Join(
                circleImage.DOFade(0f, releaseTime).SetEase(Ease.OutQuad)
            );
            yield return _sequence.WaitForCompletion();
        }

        /// <summary>
        /// vector_note: 矢印がdirectionの向きに回転して表示。
        /// onset前: 矢印が点滅しながら方向を示す
        /// sharpはパキっと点滅、roundはふわっとフェード
        /// </summary>
        private IEnumerator VectorNote(float timeUntilOnset, float direction, bool isRound)
        {
            if (arrowImage == null) yield break;

            arrowImage.gameObject.SetActive(true);

            // direction: 0=上, 時計回り → Unityの回転(Z軸)に変換
            // UnityのZ回転は反時計回りが正なので符号反転
            float zRot = -direction;
            arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRot);

            // onset前: 点滅
            float blinkCycle = isRound ? 0.5f : 0.3f;
            float elapsed = 0f;
            _sequence = DOTween.Sequence();

            while (elapsed < timeUntilOnset)
            {
                float fadeOutTime = blinkCycle * (isRound ? 0.6f : 0.4f);
                float fadeInTime = blinkCycle - fadeOutTime;

                _sequence.Append(arrowImage.DOFade(0.2f, fadeOutTime).SetEase(Ease.InOutSine));
                _sequence.Append(arrowImage.DOFade(1.0f, fadeInTime).SetEase(Ease.InOutSine));
                elapsed += blinkCycle;
            }

            yield return new WaitForSeconds(timeUntilOnset);
            _sequence.Kill();

            // onset: 矢印がスケールアップしてパルス
            arrowImage.color = WithAlpha(arrowImage.color, 1f);
            arrowImage.transform
                .DOPunchScale(Vector3.one * 0.3f, 0.15f, 1)
                .SetEase(Ease.OutQuad);
        }

        // -------------------------------------------------------
        // ユーティリティ
        // -------------------------------------------------------

        private void ResetAll()
        {
            if (circleImage != null)
            {
                circleImage.gameObject.SetActive(false);
                circleImage.transform.localScale = Vector3.one;
                circleImage.fillAmount = 1f;
                circleImage.type = Image.Type.Simple;
            }
            if (arrowImage != null)
            {
                arrowImage.gameObject.SetActive(false);
                arrowImage.transform.localScale = Vector3.one;
            }
            foreach (var line in lineImages)
            {
                if (line != null) line.gameObject.SetActive(false);
            }
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return fallback;
        }

        private static Color WithAlpha(Color c, float a) =>
            new Color(c.r, c.g, c.b, a);
    }
}