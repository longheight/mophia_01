using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Mophia.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Mophia.Input
{
    public class InputHandler : MonoBehaviour
    {
        [Header("判定ウィンドウ(秒)")]
        [SerializeField] private float perfectWindow = 0.05f;
        [SerializeField] private float greatWindow   = 0.10f;
        [SerializeField] private float goodWindow    = 0.15f;
        [SerializeField] private float badWindow     = 0.18f;

        [Header("タッチ判定半径(px)")]
        [SerializeField] private float hitRadius = 120f;

        // 早押し側の判定窓に入ったタイミングでノーツを登録する側(NoteView)が参照する
        public float BadWindow => badWindow;

        private readonly List<NoteView> _activeNotes = new();
        private readonly Dictionary<int, (Vector2 pos, float time)> _touchStart = new();
        private const float FlickMinSpeed = 300f;

        // マウス用
        private Vector2 _mouseDownPos;
        private float   _mouseDownTime;
        private bool    _mouseDown;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerUp   += OnFingerUp;
        }

        private void OnDisable()
        {
            Touch.onFingerDown -= OnFingerDown;
            Touch.onFingerUp   -= OnFingerUp;
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            // エディタ・PC向けマウス入力フォールバック
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _mouseDownPos  = Mouse.current.position.ReadValue();
                _mouseDownTime = Time.time;
                _mouseDown     = true;
                TryJudge(_mouseDownPos);
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame && _mouseDown)
            {
                _mouseDown = false;
                Vector2 upPos  = Mouse.current.position.ReadValue();
                Vector2 delta  = upPos - _mouseDownPos;
                float duration = Time.time - _mouseDownTime;
                float speed    = delta.magnitude / Mathf.Max(duration, 0.001f);

                if (speed >= FlickMinSpeed)
                {
                    float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    TryJudgeFlick(upPos, angle);
                }
            }
        }

        public void Register(NoteView note)
        {
            if (!_activeNotes.Contains(note))
                _activeNotes.Add(note);
        }

        public void Unregister(NoteView note)
        {
            _activeNotes.Remove(note);
        }

        private void OnFingerDown(Finger finger)
        {
            _touchStart[finger.index] = (finger.screenPosition, Time.time);
            TryJudge(finger.screenPosition);
        }

        private void OnFingerUp(Finger finger)
        {
            if (!_touchStart.TryGetValue(finger.index, out var start)) return;

            Vector2 delta    = finger.screenPosition - start.pos;
            float   duration = Time.time - start.time;
            float   speed    = delta.magnitude / Mathf.Max(duration, 0.001f);

            if (speed >= FlickMinSpeed)
            {
                float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                TryJudgeFlick(finger.screenPosition, angle);
            }

            _touchStart.Remove(finger.index);
        }

        private void TryJudge(Vector2 screenPos)
        {
            NoteView best     = null;
            float    bestDist = float.MaxValue;

            foreach (var note in _activeNotes)
            {
                if (note == null) continue;
                if (note.NoteType == "flick") continue;

                Vector2 noteScreen = RectTransformUtility.WorldToScreenPoint(null, note.transform.position);
                float dist = Vector2.Distance(screenPos, noteScreen);

                if (dist < hitRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = note;
                }
            }

            if (best != null)
            {
                best.OnInput(CalcResult(best.TimeUntilOnset));
                _activeNotes.Remove(best);
            }
        }

        private void TryJudgeFlick(Vector2 screenPos, float direction)
        {
            NoteView best     = null;
            float    bestDist = float.MaxValue;

            foreach (var note in _activeNotes)
            {
                if (note == null) continue;
                if (note.NoteType != "flick") continue;

                Vector2 noteScreen = RectTransformUtility.WorldToScreenPoint(null, note.transform.position);
                float dist = Vector2.Distance(screenPos, noteScreen);

                if (dist < hitRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = note;
                }
            }

            if (best != null)
            {
                best.OnInput(CalcResult(best.TimeUntilOnset));
                _activeNotes.Remove(best);
            }
        }

        private JudgeResult CalcResult(float timeUntilOnset)
        {
            float diff = Mathf.Abs(timeUntilOnset);
            if (diff <= perfectWindow) return JudgeResult.Perfect;
            if (diff <= greatWindow)   return JudgeResult.Great;
            if (diff <= goodWindow)    return JudgeResult.Good;
            if (diff <= badWindow)     return JudgeResult.Bad;
            return JudgeResult.Miss;
        }
    }

    public enum JudgeResult { Perfect, Great, Good, Bad, Miss }
}
