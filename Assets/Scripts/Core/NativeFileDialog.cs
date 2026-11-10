using System.Runtime.InteropServices;

namespace Mophia.Core
{
    /// <summary>
    /// Windows標準のファイル選択ダイアログをそのまま呼び出す(Editor/ビルド後の.exeどちらでも動く)。
    /// </summary>
    public static class NativeFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int      lStructSize;
            public System.IntPtr hwndOwner;
            public System.IntPtr hInstance;
            public string   lpstrFilter;
            public string   lpstrCustomFilter;
            public int      nMaxCustFilter;
            public int      nFilterIndex;
            public string   lpstrFile;
            public int      nMaxFile;
            public string   lpstrFileTitle;
            public int      nMaxFileTitle;
            public string   lpstrInitialDir;
            public string   lpstrTitle;
            public int      Flags;
            public short    nFileOffset;
            public short    nFileExtension;
            public string   lpstrDefExt;
            public System.IntPtr lCustData;
            public System.IntPtr lpfnHook;
            public string   lpTemplateName;
            public System.IntPtr pvReserved;
            public int      dwReserved;
            public int      FlagsEx;
        }

        private const int OFN_FILEMUSTEXIST = 0x00001000;
        // ダイアログが選択フォルダにカレントディレクトリを勝手に変更してしまうのを防ぐ
        private const int OFN_NOCHANGEDIR   = 0x00000008;

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileName([In, Out] ref OpenFileName ofn);

        /// <summary>
        /// filter例: "MP4 Files\0*.mp4\0\0" (null区切り、末尾は空文字列2つ)
        /// </summary>
        public static string OpenFile(string filter, string title = "ファイルを選択")
        {
            // lpstrFileは選択結果の書き込み先になるので、あらかじめ固定長のバッファを渡しておく
            var ofn = new OpenFileName
            {
                lpstrFilter = filter,
                lpstrFile   = new string(new char[260]),
                nMaxFile    = 260,
                lpstrTitle  = title,
                Flags       = OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR,
            };
            ofn.lStructSize = Marshal.SizeOf(ofn);

            return GetOpenFileName(ref ofn) ? ofn.lpstrFile.TrimEnd('\0') : null;
        }
    }
}
