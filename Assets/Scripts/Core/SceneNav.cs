using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mophia.Core
{
    /// <summary>
    /// ボタンのonClickから呼ぶだけの薄いシーン遷移ヘルパー。
    /// </summary>
    public class SceneNav : MonoBehaviour
    {
        public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
