using System.Collections;
using UnityEngine;
using Mophia.Data;
using Mophia.Input;
using Mophia.Core;

namespace Mophia.UI
{
    [RequireComponent(typeof(IndicatorController))]
    [RequireComponent(typeof(EffectPlayer))]
    public class NoteView : MonoBehaviour
    {
        public string NoteType       { get; private set; }
        public float  TimeUntilOnset { get; private set; }

        private NoteData            _note;
        private string              _preset;
        private ColorData           _color;
        private IndicatorController _indicator;
        private EffectPlayer        _effect;
        private InputHandler        _inputHandler;
        private UIManager           _uiManager;
        private NotesRunner         _runner;
        private bool                _judged;
        private Coroutine           _updateRoutine;

        private void Awake()
        {
            _indicator = GetComponent<IndicatorController>();
            _effect    = GetComponent<EffectPlayer>();
        }

        public void Setup(NoteData note, string preset, ColorData color, float timeUntilOnset, InputHandler inputHandler, UIManager uiManager, NotesRunner runner)
        {
            _note          = note;
            _preset        = preset;
            _color         = color;
            TimeUntilOnset = timeUntilOnset;
            NoteType       = note.noteType;
            _inputHandler  = inputHandler;
            _uiManager     = uiManager;
            _runner        = runner;
            _judged        = false;

            float cx = note.position.x - 960f;
            float cy = -(note.position.y - 540f);
            GetComponent<RectTransform>().anchoredPosition = new Vector2(cx, cy);

            if (_indicator == null)
            {
                Debug.LogError("[NoteView] _indicator が null");
                return;
            }

            _indicator.Play(preset, color, timeUntilOnset, note.direction, note.duration);

            if (_updateRoutine != null) StopCoroutine(_updateRoutine);
            _updateRoutine = StartCoroutine(UpdateTimeAndRegister());
        }

        private IEnumerator UpdateTimeAndRegister()
        {
            // onsetちょうどの1フレームだけ登録すると、その1フレームより早い/遅いタップが
            // 全て無視され「押しても反応しない」→badWindow経過後の自動Missとして遅れて
            // 消えるだけに見える(shrinkで顕著な「謎の溜め」の正体はこれ)。
            // badWindow(判定される最遅ライン)と対称にonset前から登録しておくことで、
            // 実際のタップ窓(perfect/good/bad)全域で即座に判定できるようにする。
            float badWindow = _inputHandler != null ? _inputHandler.BadWindow : 0.18f;

            while (TimeUntilOnset > badWindow)
            {
                TimeUntilOnset -= Time.deltaTime;
                yield return null;
            }
            _inputHandler?.Register(this);

            while (TimeUntilOnset > -badWindow)
            {
                TimeUntilOnset -= Time.deltaTime;
                yield return null;
            }

            if (!_judged) OnInput(JudgeResult.Miss);
        }

        public void OnInput(JudgeResult result)
        {
            if (_judged) return;
            _judged = true;

            _inputHandler?.Unregister(this);
            _indicator.Dismiss();
            _uiManager?.RegisterJudgment(result, GetComponent<RectTransform>());

            if (result != JudgeResult.Miss)
                _effect.Play(_note.images);

            StartCoroutine(ReturnToPoolAfterEffect());
        }

        private IEnumerator ReturnToPoolAfterEffect()
        {
            // エフェクト再生時間だけ待ってからプールに返却
            yield return new WaitForSeconds(2f);
            _runner?.ReturnToPool(this);
        }
    }
}
