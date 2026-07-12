using System.Collections.Generic;
using UnityEngine;
using Mophia.Data;
using Mophia.Input;
using Mophia.UI;

namespace Mophia.Core
{
    public class NotesRunner : MonoBehaviour
    {
        [SerializeField] private GameObject  notePrefab;
        [SerializeField] private Transform   noteParent;
        [SerializeField] private InputHandler inputHandler;

        [SerializeField] private float spawnLeadTime = 1.0f;

        private ChartData _chart;
        private float     _gameTime;
        private bool      _isRunning;

        private Queue<(NoteData note, ColorData color)> _pendingNotes;

        public void Initialize(ChartData chart)
        {
            _chart        = chart;
            _pendingNotes = new Queue<(NoteData, ColorData)>();

            var allNotes = new List<(NoteData, ColorData)>();
            foreach (var section in chart.sections)
                foreach (var note in section.notes)
                    allNotes.Add((note, section.color));

            allNotes.Sort((a, b) => a.Item1.time.CompareTo(b.Item1.time));
            foreach (var item in allNotes)
                _pendingNotes.Enqueue(item);

            Debug.Log($"[NotesRunner] {_pendingNotes.Count} notes ready");
        }

        public void StartGame()
        {
            _gameTime  = 0f;
            _isRunning = true;
        }

        private void Update()
        {
            if (!_isRunning) return;
            _gameTime += Time.deltaTime;

            while (_pendingNotes.Count > 0)
            {
                var (note, color) = _pendingNotes.Peek();
                if (note.time - _gameTime > spawnLeadTime) break;

                _pendingNotes.Dequeue();
                SpawnNote(note, color);
            }

            if (_pendingNotes.Count == 0) _isRunning = false;
        }

        private void SpawnNote(NoteData note, ColorData color)
        {
            GameObject obj  = Instantiate(notePrefab, noteParent);
            var        view = obj.GetComponent<NoteView>();
            if (view != null)
                view.Setup(note, color, note.time - _gameTime, inputHandler);
        }
    }
}
