using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Mophia.UI
{
    /// <summary>
    /// NoteData.images フォルダ名を受け取り、フォルダ内のpng連番を
    /// 順にRawImageに適用してエフェクトとして再生する。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class EffectPlayer : MonoBehaviour
    {
        [SerializeField] private float frameRate = 24f;

        private RawImage _rawImage;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _rawImage.enabled = false;
        }

        public void Play(string imagesFolderName)
        {
            if (string.IsNullOrEmpty(imagesFolderName))
            {
                Debug.LogWarning("[EffectPlayer] imagesFolderName が空です");
                return;
            }
            StartCoroutine(PlayRoutine(imagesFolderName));
        }

        private IEnumerator PlayRoutine(string folderName)
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, folderName);

            // フォルダ内のpngファイル一覧を取得（名前順 = 連番順）
            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[EffectPlayer] フォルダが見つかりません: {folderPath}");
                yield break;
            }

            string[] pngPaths = Directory.GetFiles(folderPath, "*.png");
            System.Array.Sort(pngPaths);

            if (pngPaths.Length == 0)
            {
                Debug.LogWarning($"[EffectPlayer] pngが見つかりません: {folderPath}");
                yield break;
            }

            float interval = 1f / frameRate;
            _rawImage.enabled = true;

            foreach (string pngPath in pngPaths)
            {
                string uri = "file://" + pngPath;
                using UnityWebRequest req = UnityWebRequestTexture.GetTexture(uri);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    _rawImage.texture = DownloadHandlerTexture.GetContent(req);
                }
                else
                {
                    Debug.LogWarning($"[EffectPlayer] 読み込み失敗: {uri}");
                }

                yield return new WaitForSeconds(interval);
            }

            _rawImage.enabled = false;
        }
    }
}
