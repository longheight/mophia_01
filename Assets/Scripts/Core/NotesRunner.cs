using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mophia.Data;
using Mophia.UI;

namespace Mophia.Core
{
    public class NotesRunner : MonoBehaviour
    {
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private Transform noteParent;

        // ノーツが画面に出るタイミング（onset時刻の何秒前に生成するか）
        [SerializeField] private float spawnLeadTime = 1.0f;

        private ChartData _chart;
        private float _gameTime;
        private bool _isRunning;

        // (sectionId → ColorData) の引き当て用
        private Dictionary<int, ColorData> _sectionColorMap;

        // 未スポーンノーツのキュー（全セクション展開済み・time順）
        private Queue<(NoteData note, ColorData color)> _pendingNotes;

        public void Initialize(ChartData chart)
        {
            _chart = chart;
            _sectionColorMap = new Dictionary<int, ColorData>();
            _pendingNotes = new Queue<(NoteData, ColorData)>();

            // セクションを展開してtimeでソートしたキューを作る
            var allNotes = new List<(NoteData note, ColorData color)>();
            foreach (var section in chart.sections)
            {
                _sectionColorMap[section.sectionId] = section.color;
                foreach (var note in section.notes)
                    allNotes.Add((note, section.color));
            }
            allNotes.Sort((a, b) => a.note.time.CompareTo(b.note.time));
            foreach (var item in allNotes)
                _pendingNotes.Enqueue(item);

            Debug.Log($"[NotesRunner] {_pendingNotes.Count} notes ready");
        }

        public void StartGame()
        {
            _gameTime = 0f;
            _isRunning = true;
        }

        private void Update()
        {
            if (!_isRunning) return;
            _gameTime += Time.deltaTime;

            // spawnLeadTime秒先読みしてノーツを生成
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
            GameObject obj = Instantiate(notePrefab, noteParent);
            var view = obj.GetComponent<NoteView>();
            if (view != null)
                view.Setup(note, color, note.time - _gameTime);
        }
    }
}
