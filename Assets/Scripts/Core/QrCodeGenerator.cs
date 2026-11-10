using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mophia.Core
{
    /// <summary>
    /// 外部パッケージなしで完結する最小限のQRコードエンコーダ(ISO/IEC 18004のサブセット実装)。
    /// バイトモード限定・ECCレベルL固定・マスクパターン0固定・バージョン1〜5のみ対応
    /// (すべて誤り訂正ブロックが1つだけなのでインターリーブ処理が不要)。
    /// cloudflaredの発行するURL程度の文字列(100byte程度まで)を想定しており、汎用QRエンコーダではない。
    /// </summary>
    public static class QrCodeGenerator
    {
        // フォーマット情報のECCレベル指定(L=01, M=00, Q=11, H=10)。マスクは常に0固定
        private const int EccLevelBitsL = 0b01;

        // バージョン1〜5・ECC L(index0=V1): データcodeword数, 総codeword数(いずれもブロック分割なし)
        private static readonly int[] DataCodewords  = { 19, 34, 55, 80, 108 };
        private static readonly int[] TotalCodewords = { 26, 44, 70, 100, 134 };

        public static Texture2D GenerateTexture(string text, int moduleScale = 8, int quietZoneModules = 4)
        {
            if (!TryBuildMatrix(text, out bool[,] matrix, out int size))
            {
                Debug.LogError($"[QrCodeGenerator] QRコード化できません(文字列が長すぎます): {text}");
                return null;
            }

            int quietPx = quietZoneModules * moduleScale;
            int texSize = size * moduleScale + quietPx * 2;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };

            var pixels = new Color32[texSize * texSize];
            Color32 white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;

            for (int my = 0; my < size; my++)
            {
                for (int mx = 0; mx < size; mx++)
                {
                    if (!matrix[mx, my]) continue;
                    // テクスチャのY軸は下から上なので、モジュールの行(上から数えるmy)を反転して配置する
                    int py0 = quietPx + (size - 1 - my) * moduleScale;
                    int px0 = quietPx + mx * moduleScale;
                    for (int dy = 0; dy < moduleScale; dy++)
                        for (int dx = 0; dx < moduleScale; dx++)
                            pixels[(py0 + dy) * texSize + (px0 + dx)] = new Color32(0, 0, 0, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static bool TryBuildMatrix(string text, out bool[,] matrix, out int size)
        {
            matrix = null;
            size = 0;
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            int version = -1;
            for (int v = 1; v <= 5; v++)
            {
                // モード指示子4bit+文字数指示子8bit ≒ 1.5byte分のオーバーヘッド。安全側に2byte見ておく
                int capacity = DataCodewords[v - 1] - 2;
                if (bytes.Length <= capacity) { version = v; break; }
            }
            if (version < 0) return false;

            int dataCwCount  = DataCodewords[version - 1];
            int totalCwCount = TotalCodewords[version - 1];
            int eccCwCount   = totalCwCount - dataCwCount;

            byte[] dataCodewords = BuildDataCodewords(bytes, dataCwCount);
            byte[] eccCodewords  = ReedSolomon.ComputeEcc(dataCodewords, eccCwCount);

            var allCodewords = new byte[dataCwCount + eccCwCount];
            Array.Copy(dataCodewords, 0, allCodewords, 0, dataCwCount);
            Array.Copy(eccCodewords, 0, allCodewords, dataCwCount, eccCwCount);

            size = 4 * version + 17;
            matrix = new bool[size, size];
            var isFunction = new bool[size, size];

            DrawFinderPattern(matrix, isFunction, size, 0, 0);
            DrawFinderPattern(matrix, isFunction, size, size - 7, 0);
            DrawFinderPattern(matrix, isFunction, size, 0, size - 7);
            DrawTimingPatterns(matrix, isFunction, size);
            DrawAlignmentPattern(matrix, isFunction, size, version);
            ReserveFormatInfoArea(isFunction, size);
            SetModule(matrix, isFunction, 8, size - 8, true); // ダークモジュール

            DrawCodewordsZigzag(matrix, isFunction, size, allCodewords);
            ApplyMask0(matrix, isFunction, size);
            DrawFormatInfo(matrix, isFunction, size);

            return true;
        }

        // ===== データcodewordの組み立て(バイトモード) =====

        private static byte[] BuildDataCodewords(byte[] contentBytes, int dataCwCount)
        {
            var bits = new List<bool>();
            AppendBits(bits, 0b0100, 4);             // モード指示子: バイトモード
            AppendBits(bits, contentBytes.Length, 8); // 文字数指示子(バージョン1-9のバイトモードは8bit)
            foreach (byte b in contentBytes) AppendBits(bits, b, 8);

            int maxBits = dataCwCount * 8;
            int terminatorLen = Math.Min(4, maxBits - bits.Count);
            if (terminatorLen > 0) AppendBits(bits, 0, terminatorLen);
            while (bits.Count % 8 != 0) bits.Add(false);

            var codewords = new List<byte>();
            for (int i = 0; i < bits.Count; i += 8)
            {
                int v = 0;
                for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i + j] ? 1 : 0);
                codewords.Add((byte)v);
            }

            bool useEc = true;
            while (codewords.Count < dataCwCount)
            {
                codewords.Add((byte)(useEc ? 0xEC : 0x11));
                useEc = !useEc;
            }
            return codewords.ToArray();
        }

        private static void AppendBits(List<bool> bits, int value, int length)
        {
            for (int i = length - 1; i >= 0; i--)
                bits.Add(((value >> i) & 1) != 0);
        }

        // ===== 機能パターン =====

        private static void DrawFinderPattern(bool[,] matrix, bool[,] isFunction, int size, int originX, int originY)
        {
            for (int dy = -1; dy <= 7; dy++)
            {
                for (int dx = -1; dx <= 7; dx++)
                {
                    int x = originX + dx, y = originY + dy;
                    if (x < 0 || x >= size || y < 0 || y >= size) continue;

                    bool dark;
                    if (dx < 0 || dx > 6 || dy < 0 || dy > 6)
                    {
                        dark = false; // 外周1マスの白い分離パターン
                    }
                    else
                    {
                        bool onOuterRing = dx == 0 || dx == 6 || dy == 0 || dy == 6;
                        bool onInnerBox  = dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4;
                        dark = onOuterRing || onInnerBox;
                    }
                    SetModule(matrix, isFunction, x, y, dark);
                }
            }
        }

        private static void DrawTimingPatterns(bool[,] matrix, bool[,] isFunction, int size)
        {
            for (int i = 8; i <= size - 9; i++)
            {
                bool dark = i % 2 == 0;
                SetModule(matrix, isFunction, i, 6, dark);
                SetModule(matrix, isFunction, 6, i, dark);
            }
        }

        private static void DrawAlignmentPattern(bool[,] matrix, bool[,] isFunction, int size, int version)
        {
            if (version < 2) return; // V1はアライメントパターンなし

            int coord = 4 * version + 10; // V2〜V6は単一のアライメントパターンのみ、この座標に1つ
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    bool onRing   = Math.Abs(dx) == 2 || Math.Abs(dy) == 2;
                    bool onCenter = dx == 0 && dy == 0;
                    SetModule(matrix, isFunction, coord + dx, coord + dy, onRing || onCenter);
                }
            }
        }

        private static void ReserveFormatInfoArea(bool[,] isFunction, int size)
        {
            ForEachFormatBitPosition(size, (x, y, bitIndex) => isFunction[x, y] = true);
        }

        private static void DrawFormatInfo(bool[,] matrix, bool[,] isFunction, int size)
        {
            int formatBits = ComputeFormatBits(EccLevelBitsL, MaskPattern0);
            ForEachFormatBitPosition(size, (x, y, bitIndex) =>
            {
                matrix[x, y] = ((formatBits >> bitIndex) & 1) != 0;
            });
        }

        private const int MaskPattern0 = 0b000;

        /// <summary>
        /// 15bitフォーマット情報を計算する(ECCレベル2bit+マスク3bitの5bitデータ → BCH(15,5)誤り訂正10bit
        /// → 標準マスク定数0x5412とXOR)。生成多項式はISO/IEC 18004で規定されたx^10+x^8+x^5+x^4+x^2+x+1(=0x537)。
        /// </summary>
        private static int ComputeFormatBits(int eccLevelBits2, int maskBits3)
        {
            const int generator = 0b10100110111; // 0x537, 次数10
            const int xorMask   = 0b101010000010010; // 0x5412

            int data = (eccLevelBits2 << 3) | maskBits3; // 5bit
            int value = data << 10; // 上位5bitがdata、下位10bitに0除算の余りを求めていく
            for (int i = 14; i >= 10; i--)
            {
                if (((value >> i) & 1) != 0)
                    value ^= generator << (i - 10);
            }
            int remainder = value & 0x3FF;
            return ((data << 10) | remainder) ^ xorMask;
        }

        /// <summary>
        /// フォーマット情報15bit(bit0=LSB)の配置座標を、1つ目のコピー・2つ目のコピーそれぞれで
        /// bitIndex 0..14の順に列挙する(2つのコピーは同じ15bitを表す)。
        /// 座標式はNayukiのQR-Code-generator(MIT)のdrawFormatBits相当を参照して独自実装したもの。
        /// </summary>
        private static void ForEachFormatBitPosition(int size, Action<int, int, int> visit)
        {
            // 1つ目のコピー(左上フィンダー周り) bit0..14
            for (int i = 0; i <= 5; i++) visit(8, i, i);
            visit(8, 7, 6);
            visit(8, 8, 7);
            visit(7, 8, 8);
            for (int i = 9; i <= 14; i++) visit(14 - i, 8, i);

            // 2つ目のコピー(右上+左下) bit0..14
            for (int i = 0; i < 8; i++) visit(size - 1 - i, 8, i);
            for (int i = 8; i < 15; i++) visit(8, size - 15 + i, i);
        }

        // ===== データ配置(ジグザグスキャン) =====

        private static void DrawCodewordsZigzag(bool[,] matrix, bool[,] isFunction, int size, byte[] codewords)
        {
            int bitIndex = 0;
            int totalBits = codewords.Length * 8;

            bool GetBit()
            {
                if (bitIndex >= totalBits) { bitIndex++; return false; } // 余りビットは0
                bool b = ((codewords[bitIndex / 8] >> (7 - bitIndex % 8)) & 1) != 0;
                bitIndex++;
                return b;
            }

            bool upward = true;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5; // 縦タイミングパターンの列(col6)は飛ばす

                for (int i = 0; i < size; i++)
                {
                    int y = upward ? (size - 1 - i) : i;
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        if (isFunction[x, y]) continue;
                        matrix[x, y] = GetBit(); // データセルはisFunctionを立てない(後段のマスク対象から漏らさないため)
                    }
                }
                upward = !upward;
            }
        }

        private static void ApplyMask0(bool[,] matrix, bool[,] isFunction, int size)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (isFunction[x, y]) continue;
                    if ((x + y) % 2 == 0) matrix[x, y] = !matrix[x, y];
                }
            }
        }

        private static void SetModule(bool[,] matrix, bool[,] isFunction, int x, int y, bool dark)
        {
            matrix[x, y] = dark;
            isFunction[x, y] = true;
        }

        /// <summary>
        /// QR仕様のReed-Solomon誤り訂正(GF(256), 原始多項式0x11D)。
        /// </summary>
        private static class ReedSolomon
        {
            private static readonly int[] GfExp = new int[512];
            private static readonly int[] GfLog = new int[256];

            static ReedSolomon()
            {
                int x = 1;
                for (int i = 0; i < 255; i++)
                {
                    GfExp[i] = x;
                    GfLog[x] = i;
                    x <<= 1;
                    if ((x & 0x100) != 0) x ^= 0x11D;
                }
                for (int i = 255; i < 512; i++) GfExp[i] = GfExp[i - 255];
            }

            private static int Mul(int a, int b)
            {
                if (a == 0 || b == 0) return 0;
                return GfExp[GfLog[a] + GfLog[b]];
            }

            private static int[] MultiplyPoly(int[] a, int[] b)
            {
                var result = new int[a.Length + b.Length - 1];
                for (int i = 0; i < a.Length; i++)
                    for (int j = 0; j < b.Length; j++)
                        result[i + j] ^= Mul(a[i], b[j]);
                return result;
            }

            private static int[] BuildGeneratorPolynomial(int eccCount)
            {
                int[] g = { 1 };
                for (int i = 0; i < eccCount; i++)
                    g = MultiplyPoly(g, new[] { 1, GfExp[i] }); // (x - α^i) = (x + α^i)
                return g; // 長さ eccCount+1, g[0]=1(最高次)
            }

            public static byte[] ComputeEcc(byte[] data, int eccCount)
            {
                int[] generator = BuildGeneratorPolynomial(eccCount);
                var remainder = new int[data.Length + eccCount];
                for (int i = 0; i < data.Length; i++) remainder[i] = data[i];

                for (int i = 0; i < data.Length; i++)
                {
                    int coef = remainder[i];
                    if (coef == 0) continue;
                    for (int j = 0; j < generator.Length; j++)
                        remainder[i + j] ^= Mul(generator[j], coef);
                }

                var ecc = new byte[eccCount];
                for (int i = 0; i < eccCount; i++) ecc[i] = (byte)remainder[data.Length + i];
                return ecc;
            }
        }
    }
}
