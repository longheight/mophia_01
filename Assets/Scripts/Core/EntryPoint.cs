using UnityEngine;
using Mophia.Data;

namespace Mophia.Core
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private ChartLoader chartLoader;
        [SerializeField] private NotesRunner notesRunner;

        private void Start()
        {
            ChartData chart = chartLoader.Load();
            if (chart == null) return;

            notesRunner.Initialize(chart);
            notesRunner.StartGame();
        }
    }
}
