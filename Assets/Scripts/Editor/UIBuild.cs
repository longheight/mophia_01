using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mophia.EditorTools
{
    /// <summary>
    /// タイトル/曲選択シーンで共通して使う、コードだけでUI部品を組み立てる小さなヘルパー集。
    /// ワイヤーフレーム風(白背景+黒線+ブラケット状の角飾り)の見た目を想定している。
    /// テキストは拡縮しても輪郭が荒れないようTextMeshProを使う。
    /// </summary>
    internal static class UIBuild
    {
        /// <summary>
        /// 21.5インチモニターで見たときと物理的に同じ大きさになるよう縮小する比率(21.5/24)。
        /// 24インチモニターのネイティブ解像度でフルスクリーン表示しつつ、中身をこの比率で
        /// 中央に縮小し、外周をカメラの黒背景で埋めることで擬似的なレターボックスを作る。
        /// OSやGPU側のフルスクリーン解像度切り替え+スケーリングだと環境によって引き伸ばされて
        /// しまう(黒帯にならない)ことがあったため、Unity側で完結させている。
        /// </summary>
        public const float LetterboxScale = 21.5f / 24f;

        /// <summary>
        /// Canvas直下の既存の子を全て、この比率で縮小された白背景の新しい親(LetterboxRoot)配下に
        /// つけ替える。中身は元のレイアウトのまま中央に縮小表示され、はみ出した周囲は
        /// (カメラ背景色を黒にしておけば)黒帯になる。
        /// </summary>
        public static void ApplyLetterbox(GameObject canvasGo, float scale = LetterboxScale)
        {
            var rootGo = new GameObject("LetterboxRoot", typeof(RectTransform), typeof(Image));
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.SetParent(canvasGo.transform, false);
            rootRt.SetAsFirstSibling();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootGo.GetComponent<Image>().color = Color.white;

            // 背景パーティクル(BackgroundParticles.cs)の投影先。白背景の直後・実コンテンツの手前に
            // 置くことで、レターボックス化後も白背景の上にパーティクルが正しく見えるようにする
            var particleDisplayGo = new GameObject("ParticleDisplay", typeof(RectTransform), typeof(RawImage));
            var particleDisplayRt = (RectTransform)particleDisplayGo.transform;
            particleDisplayRt.SetParent(rootRt, false);
            particleDisplayRt.anchorMin = Vector2.zero;
            particleDisplayRt.anchorMax = Vector2.one;
            particleDisplayRt.offsetMin = Vector2.zero;
            particleDisplayRt.offsetMax = Vector2.zero;
            var particleDisplayImage = particleDisplayGo.GetComponent<RawImage>();
            particleDisplayImage.color = Color.white; // テクスチャの透過をそのまま活かす(RenderTextureは実行時に割り当てられる)
            particleDisplayImage.raycastTarget = false;

            var existingChildren = new List<Transform>();
            foreach (Transform child in canvasGo.transform)
                if (child != rootRt) existingChildren.Add(child);

            // worldPositionStaysをtrueにして、スケールを掛ける前に(見た目を変えずに)付け替える
            foreach (var child in existingChildren)
                child.SetParent(rootRt, worldPositionStays: true);

            rootRt.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// 指定パスにTMP_FontAssetが無ければ、元のTTF/OTFから生成してキャッシュする。
        /// </summary>
        public static TMP_FontAsset GetOrCreateFontAsset(string fontAssetPath, string sourceFontPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (existing != null) return existing;

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[UIBuild] フォントが見つかりません: {sourceFontPath}");
                return null;
            }

            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (created == null)
            {
                Debug.LogError($"[UIBuild] TMP_FontAssetの生成に失敗しました: {sourceFontPath}");
                return null;
            }

            // CreateFontAsset()が作るアトラステクスチャ/マテリアルはメモリ上だけの存在なので、
            // サブアセットとして明示的に保存しないとドメインリロード後に参照切れ("...is missing")になる。
            AssetDatabase.CreateAsset(created, fontAssetPath);

            if (created.atlasTexture != null)
                AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
            if (created.material != null)
                AssetDatabase.AddObjectToAsset(created.material, created);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(created));
            return created;
        }

        /// <summary>
        /// fontにfallbackの文字が無い場合、自動的にfallbackで描画するよう設定する。
        /// 楽曲タイトルはユーザー入力で欧文/和文どちらが来るか分からないため、欧文フォント(Agency FB)に
        /// 和文フォント(Yu Gothic)をフォールバックとして登録し、どちらの文字でも文字化け(□□□)しないようにする。
        /// </summary>
        public static void SetFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null || font == fallback) return;

            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (font.fallbackFontAssetTable.Contains(fallback)) return;

            font.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        public static TMP_Text Text(Transform parent, string name, string content, TMP_FontAsset font, int fontSize,
            Color color, Vector2 pivot, Vector2 pos, Vector2 size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = pivot;
            rt.anchorMax = pivot;
            rt.pivot     = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text      = content;
            if (font != null) text.font = font;
            text.fontSize  = fontSize;
            text.alignment = alignment;
            text.color     = color;

            return text;
        }

        /// <summary>
        /// 二重構造のボタンを作る: 内側に実際にクリックできる枠線ボックス(背景+完全な罫線+テキスト)、
        /// その外側に少し隙間を空けて浮いた角ブラケット(四隅のL字マーク)を配置する。
        /// 中央(0.5,0.5)基準で配置する。
        /// </summary>
        public static Button BracketButton(Transform parent, string name, string label, TMP_FontAsset font,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = Color.white;

            AddBorderFrame(rt, size, thickness: 4f, color: Color.black);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text      = label;
            if (font != null) text.font = font;
            text.fontSize  = 30;
            text.alignment = TextAlignmentOptions.Center;
            text.color     = Color.black;

            // 外側の角ブラケットは内側ボックスより一回り大きく、隙間を空けて浮かせる
            AddCornerBrackets(rt, margin: 18f, armLength: 28f, thickness: 4f, color: Color.black);

            return go.GetComponent<Button>();
        }

        /// <summary>矩形の完全な罫線を4本のバーで作る(Outlineコンポーネントは単色塗りの矩形には効かないため)。</summary>
        public static void AddBorderFrame(RectTransform parent, Vector2 size, float thickness, Color color)
        {
            CreateBar(parent, new Vector2(0,  size.y / 2f), new Vector2(size.x, thickness), color); // top
            CreateBar(parent, new Vector2(0, -size.y / 2f), new Vector2(size.x, thickness), color); // bottom
            CreateBar(parent, new Vector2(-size.x / 2f, 0), new Vector2(thickness, size.y), color); // left
            CreateBar(parent, new Vector2( size.x / 2f, 0), new Vector2(thickness, size.y), color); // right
        }

        private static void AddCornerBrackets(RectTransform buttonRt, float margin, float armLength, float thickness, Color color)
        {
            float halfW = buttonRt.sizeDelta.x / 2f + margin;
            float halfH = buttonRt.sizeDelta.y / 2f + margin;

            AddCornerMark(buttonRt, new Vector2(-halfW,  halfH), armLength, thickness, color, hDir:  1, vDir: -1);
            AddCornerMark(buttonRt, new Vector2( halfW,  halfH), armLength, thickness, color, hDir: -1, vDir: -1);
            AddCornerMark(buttonRt, new Vector2(-halfW, -halfH), armLength, thickness, color, hDir:  1, vDir:  1);
            AddCornerMark(buttonRt, new Vector2( halfW, -halfH), armLength, thickness, color, hDir: -1, vDir:  1);
        }

        private static void AddCornerMark(RectTransform parent, Vector2 cornerPos, float armLength, float thickness,
            Color color, int hDir, int vDir)
        {
            CreateBar(parent, cornerPos + new Vector2(armLength * hDir / 2f, 0), new Vector2(armLength, thickness), color);
            CreateBar(parent, cornerPos + new Vector2(0, armLength * vDir / 2f), new Vector2(thickness, armLength), color);
        }

        private static void CreateBar(RectTransform parent, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
        }
    }
}
