using System.IO;
using UnityEngine;
using Mophia.Data;

namespace Mophia.Core
{
    public class ChartLoader : MonoBehaviour
    {
        // StreamingAssets直下のJSONファイル名
        [SerializeField] private string chartFileName = "chart.json";

        public ChartData Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, chartFileName);

            if (!File.Exists(path))
            {
                Debug.LogError($"[ChartLoader] chart.json が見つかりません: {path}");
                return null;
            }

            string json = File.ReadAllText(path);
            ChartData data = JsonUtility.FromJson<ChartData>(json);

            if (data == null)
            {
                Debug.LogError("[ChartLoader] JSONのパースに失敗しました");
                return null;
            }

            Debug.Log($"[ChartLoader] 読み込み完了: {data.sections?.Count} sections");
            return data;
        }
    }
}
