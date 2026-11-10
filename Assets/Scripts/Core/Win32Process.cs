using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Mophia.Core
{
    /// <summary>
    /// Win32 CreateProcessを直接呼ぶための最小ラッパー。
    /// System.Diagnostics.ProcessのWorkingDirectory指定だと呼び出し元(Unity)の
    /// カレントディレクトリまで巻き込まれてしまう問題を避けるために使う。
    /// 短命プロセス(Pythonスクリプト等)を待つRunAndWaitと、cloudflaredのように
    /// 起動しっぱなしにするプロセス用のStartBackgroundの2種類を提供する。
    /// </summary>
    internal static class Win32Process
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int    cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int    dwX, dwY, dwXSize, dwYSize;
            public int    dwXCountChars, dwYCountChars;
            public int    dwFillAttribute, dwFlags;
            public short  wShowWindow, cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int    dwProcessId, dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int    nLength;
            public IntPtr lpSecurityDescriptor;
            public bool   bInheritHandle;
        }

        private const uint CREATE_NO_WINDOW     = 0x08000000;
        private const uint INFINITE             = 0xFFFFFFFF;
        private const uint STARTF_USESTDHANDLES = 0x00000100;
        private const uint HANDLE_FLAG_INHERIT  = 1;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        /// <summary>起動済みプロセスへのハンドル。長時間起動しっぱなしにするプロセス(cloudflared等)向け。</summary>
        public sealed class Handle
        {
            private readonly IntPtr _hProcess;
            private readonly IntPtr _hThread;
            private int _closed;

            internal Handle(IntPtr hProcess, IntPtr hThread)
            {
                _hProcess = hProcess;
                _hThread  = hThread;
            }

            public void Kill()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0) return;
                try { TerminateProcess(_hProcess, 0); } catch { /* 終了処理なので握りつぶす */ }
                CloseHandle(_hProcess);
                CloseHandle(_hThread);
            }
        }

        private static bool CreateWithPipe(string commandLine, string workingDirectory,
            out PROCESS_INFORMATION pi, out IntPtr stdOutRead)
        {
            pi = default;
            stdOutRead = IntPtr.Zero;

            var sa = new SECURITY_ATTRIBUTES
            {
                nLength              = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
                bInheritHandle       = true,
                lpSecurityDescriptor = IntPtr.Zero,
            };

            if (!CreatePipe(out IntPtr readHandle, out IntPtr writeHandle, ref sa, 0))
                return false;

            // 読み取り側ハンドルは子プロセスに継承させない(継承すると子プロセス終了後もパイプが閉じない)
            SetHandleInformation(readHandle, HANDLE_FLAG_INHERIT, 0);

            var si = new STARTUPINFO();
            si.cb         = Marshal.SizeOf(si);
            si.dwFlags    = (int)STARTF_USESTDHANDLES;
            si.hStdOutput = writeHandle;
            si.hStdError  = writeHandle;
            si.hStdInput  = IntPtr.Zero;

            bool started = CreateProcess(
                null, commandLine, IntPtr.Zero, IntPtr.Zero,
                true, CREATE_NO_WINDOW, IntPtr.Zero, workingDirectory,
                ref si, out pi);

            // 親側の書き込みハンドルはここで閉じる(子だけが書き込む側を持つようにする。
            // 閉じないと子プロセス終了後もReadFileがEOFを検知できずブロックし続ける)
            CloseHandle(writeHandle);

            if (!started)
            {
                CloseHandle(readHandle);
                return false;
            }

            stdOutRead = readHandle;
            return true;
        }

        private static void PumpLines(IntPtr stdOutRead, Action<string> onOutputLine)
        {
            var lineBuffer = new StringBuilder();
            var buffer     = new byte[4096];
            while (true)
            {
                bool ok = ReadFile(stdOutRead, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero);
                if (!ok || bytesRead == 0) break; // 子プロセス側が閉じた(=終了、またはKillされた)

                string chunk = Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                foreach (char c in chunk)
                {
                    if (c == '\n')
                    {
                        onOutputLine?.Invoke(lineBuffer.ToString().TrimEnd('\r'));
                        lineBuffer.Clear();
                    }
                    else
                    {
                        lineBuffer.Append(c);
                    }
                }
            }
            if (lineBuffer.Length > 0) onOutputLine?.Invoke(lineBuffer.ToString());
            CloseHandle(stdOutRead);
        }

        /// <summary>
        /// 子プロセスを起動し、標準出力をパイプ越しに1行ずつ読み取りながら終了を待つ(短命プロセス向け)。
        /// onOutputLineは出力を読み取っているバックグラウンドスレッドから同期的に呼ばれる。
        /// </summary>
        public static bool RunAndWait(string commandLine, string workingDirectory, Action<string> onOutputLine = null)
        {
            if (!CreateWithPipe(commandLine, workingDirectory, out PROCESS_INFORMATION pi, out IntPtr stdOutRead))
                return false;

            PumpLines(stdOutRead, onOutputLine);

            WaitForSingleObject(pi.hProcess, INFINITE);
            GetExitCodeProcess(pi.hProcess, out uint exitCode);
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);

            return exitCode == 0;
        }

        /// <summary>
        /// 子プロセスを起動し、終了を待たずに返す(cloudflaredのように起動しっぱなしにするプロセス向け)。
        /// 出力の読み取りは別スレッドで継続し、onOutputLineをそのスレッドから呼び続ける。
        /// 呼び出し側は不要になったらHandle.Kill()で明示的に終了させること。
        /// </summary>
        public static Handle StartBackground(string commandLine, string workingDirectory, Action<string> onOutputLine = null)
        {
            if (!CreateWithPipe(commandLine, workingDirectory, out PROCESS_INFORMATION pi, out IntPtr stdOutRead))
                return null;

            var thread = new Thread(() => PumpLines(stdOutRead, onOutputLine)) { IsBackground = true };
            thread.Start();

            return new Handle(pi.hProcess, pi.hThread);
        }
    }
}
