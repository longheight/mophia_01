using System.Collections;
using UnityEngine;
using Mophia.Data;
using Mophia.Input;

namespace Mophia.UI
{
    [RequireComponent(typeof(IndicatorController))]
    [RequireComponent(typeof(EffectPlayer))]
    public class NoteView : MonoBehaviour
    {
        public string NoteType       { get; private set; }
        public float  TimeUntilOnset { get; private set; }

        private NoteData            _note;
        private ColorData           _color;
        private IndicatorController _indicator;
        private EffectPlayer        _effect;
        private InputHandler        _inputHandler;
        private bool                _judged;

        private void Awake()
        {
            _indicator = GetComponent<IndicatorController>();
            _effect    = GetComponent<EffectPlayer>();
        }

        public void Setup(NoteData note, ColorData color, float timeUntilOnset, InputHandler inputHandler)
        {
            _note          = note;
            _color         = color;
            TimeUntilOnset = timeUntilOnset;
            NoteType       = note.noteType;
            _inputHandler  = inputHandler;
            _judged        = false;

            Debug.Log($"[NoteView] Setup: index={note.index} preset={note.preset} timeUntilOnset={timeUntilOnset:F2}");

            // 映像ピクセル座標(1920x1080基準) → Canvas座標(中心原点)に変換
            float cx = note.position.x - 960f;
            float cy = -(note.position.y - 540f); // Y軸反転
            GetComponent<RectTransform>().anchoredPosition = new Vector2(cx, cy);

            if (_indicator == null)
            {
                Debug.LogError("[NoteView] _indicator が null");
                return;
            }

            _indicator.Play(note.preset, color, timeUntilOnset, note.direction, note.duration);
            StartCoroutine(UpdateTimeAndRegister());
        }

        private IEnumerator UpdateTimeAndRegister()
        {
            while (TimeUntilOnset > 0f)
            {
                TimeUntilOnset -= Time.deltaTime;
                yield return null;
            }
            TimeUntilOnset = 0f;
            _inputHandler?.Register(this);

            // Miss猶予: badWindow後に自動Miss
            yield return new WaitForSeconds(0.18f);
            if (!_judged) OnInput(JudgeResult.Miss);
        }

        public void OnInput(JudgeResult result)
        {
            if (_judged) return;
            _judged = true;

            _inputHandler?.Unregister(this);
            _indicator.Stop();

            Debug.Log($"[NoteView] index={_note.index} result={result}");

            _effect.Play(_note.images);
            StartCoroutine(DestroyAfterEffect());
        }

        private IEnumerator DestroyAfterEffect()
        {
            yield return new WaitForSeconds(2f);
            Destroy(gameObject);
        }
    }
}
