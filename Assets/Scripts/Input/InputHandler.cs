using UnityEngine;

namespace Mophia.Input
{
    /// <summary>
    /// タッチ・マウス入力を受け取り、noteTypeに対応した判定を行う。
    /// 判定結果を NoteView.OnInput() に通知する。
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        // TODO: 判定ウィンドウ（ms）
        [SerializeField] private float perfectWindow = 0.05f;
        [SerializeField] private float goodWindow    = 0.10f;

        private void Update()
        {
            // TODO: Input.touches または Mouse ベースの入力処理
            // tap: TouchPhase.Began → 短時間でEnded
            // flick: Began→Ended 間の速度ベクトル → direction算出
            // long: Began から duration秒間 Stationary/Moved → Ended
            // touch: 広域タッチ（複数点）
        }
    }
}
