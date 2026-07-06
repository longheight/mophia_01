using System.Collections;
using UnityEngine;
using Mophia.Data;

namespace Mophia.UI
{
    /// <summary>
    /// ノーツ1個のライフサイクル管理。
    /// - Setup() でデータを受け取り、onset時刻まで IndicatorController を動かす
    /// - onset到達で入力受付 → 入力後 EffectPlayer でエフェクト再生
    /// </summary>
    [RequireComponent(typeof(IndicatorController))]
    [RequireComponent(typeof(EffectPlayer))]
    public class NoteView : MonoBehaviour
    {
        private NoteData _note;
        private ColorData _color;
        private float _timeUntilOnset;

        private IndicatorController _indicator;
        private EffectPlayer _effect;

        private void Awake()
        {
            _indicator = GetComponent<IndicatorController>();
            _effect    = GetComponent<EffectPlayer>();
        }

        public void Setup(NoteData note, ColorData color, float timeUntilOnset)
        {
            _note          = note;
            _color         = color;
            _timeUntilOnset = timeUntilOnset;

            Debug.Log($"[NoteView] Setup called: index={note.index} preset={note.preset} timeUntilOnset={timeUntilOnset:F2}");

            // 座標セット（スクリーン座標→ワールド座標変換は UIManager 等で調整）
            transform.position = new Vector3(note.position.x, note.position.y, 0f);

            if (_indicator == null)
            {
                Debug.LogError("[NoteView] _indicator が null です。IndicatorController がアタッチされているか確認してください。");
                return;
            }

            _indicator.Play(note.preset, color, timeUntilOnset, note.direction, note.duration);

            StartCoroutine(WaitForOnset());
        }

        private IEnumerator WaitForOnset()
        {
            yield return new WaitForSeconds(_timeUntilOnset);
            // TODO: 入力受付ロジックを InputHandler と接続
            OnInput();
        }

        // 入力受付後の処理（InputHandlerから呼び出す想定）
        public void OnInput()
        {
            _indicator.Stop();
            _effect.Play(_note.images);
            // TODO: スコアリング通知
            StartCoroutine(DestroyAfterEffect());
        }

        private IEnumerator DestroyAfterEffect()
        {
            yield return new WaitForSeconds(2f); // エフェクト再生時間の上限
            Destroy(gameObject);
        }
    }
}
