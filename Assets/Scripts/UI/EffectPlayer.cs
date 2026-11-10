using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Mophia.Core;

namespace Mophia.UI
{
    /// <summary>
    /// スプライトシート + offset JSON を使ってエフェクトを再生する。
    ///
    /// StreamingAssets/<imagesFolderName>/
    ///   atlas_0000.png   ← スプライトシート
    ///   offset_0000.json ← フレームUV・サイズ情報
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class EffectPlayer : MonoBehaviour
    {
        [SerializeField] private float frameRate = 24f;

        private RawImage      _rawImage;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rawImage      = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();
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
            string folderPath = Path.Combine(SongSelection.GetSongDirectory(), folderName);

            // ---- 1. offset JSON を読み込む ----
            // (UnityWebRequestはローカルファイルでも内部的に重いので、通常のFile I/Oにする)
            string offsetPath = Path.Combine(folderPath, "offset.json");
            if (!File.Exists(offsetPath))
            {
                Debug.LogWarning($"[EffectPlayer] offset JSON 読み込み失敗: {offsetPath}");
                yield break;
            }
            string offsetJson = File.ReadAllText(offsetPath);

            OffsetData offsetData = JsonUtility.FromJson<OffsetData>(offsetJson);
            if (offsetData == null || offsetData.frames == null || offsetData.frames.Length == 0)
            {
                Debug.LogWarning($"[EffectPlayer] offset JSON のパース失敗: {offsetPath}");
                yield break;
            }

            // ---- 2. アトラスPNG を読み込む ----
            string atlasPath = Path.Combine(folderPath, "atlas.png");
            if (!File.Exists(atlasPath))
            {
                Debug.LogWarning($"[EffectPlayer] atlas PNG 読み込み失敗: {atlasPath}");
                yield break;
            }
            byte[] atlasBytes = File.ReadAllBytes(atlasPath);
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(atlasBytes);

            // ---- 3. フレームを順に表示（時刻ベース） ----
            _rawImage.texture = atlas;
            _rawImage.enabled = true;

            float interval = 1f / frameRate;
            float atlasW   = offsetData.atlasWidth;
            float atlasH   = offsetData.atlasHeight;
            int   total    = offsetData.frames.Length;

            float startTime = Time.realtimeSinceStartup;
            int   lastFrame = -1;

            while (true)
            {
                float elapsed    = Time.realtimeSinceStartup - startTime;
                int   frameIndex = Mathf.FloorToInt(elapsed / interval);

                if (frameIndex >= total) break;

                if (frameIndex != lastFrame)
                {
                    var frame = offsetData.frames[frameIndex];

                    float u  = frame.dx / atlasW;
                    float v  = 1f - (frame.dy + frame.h) / atlasH;
                    float uw = frame.w / atlasW;
                    float vh = frame.h / atlasH;

                    _rawImage.uvRect         = new Rect(u, v, uw, vh);
                    _rectTransform.sizeDelta = new Vector2(frame.w, frame.h);
                    lastFrame = frameIndex;
                }

                yield return null;
            }

            _rawImage.enabled  = false;
            _rawImage.uvRect   = new Rect(0, 0, 1, 1);
            Destroy(atlas);
        }

        // -------------------------------------------------------
        // offset JSON デシリアライズ用クラス
        // -------------------------------------------------------

        [Serializable]
        private class OffsetData
        {
            public int          index;
            public int          frameCount;
            public int          cellWidth;
            public int          cellHeight;
            public int          cols;
            public int          rows;
            public int          atlasWidth;
            public int          atlasHeight;
            public FrameEntry[] frames;
        }

        [Serializable]
        private class FrameEntry
        {
            public int dx;  // アトラス上X
            public int dy;  // アトラス上Y
            public int w;   // 実コンテンツ幅
            public int h;   // 実コンテンツ高さ
        }
    }
}
