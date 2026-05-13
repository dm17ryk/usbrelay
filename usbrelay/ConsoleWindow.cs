using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace usbrelay
{
    internal static class ConsoleWindow
    {
        private const int ATTACH_PARENT_PROCESS = -1;
        private const int SW_HIDE = 0;
        private static bool attachedParentConsole;

        public static bool PrepareForStartup()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return true;

            if (GetConsoleWindow() == IntPtr.Zero)
            {
                attachedParentConsole = AttachConsole(ATTACH_PARENT_PROCESS);
                if (attachedParentConsole)
                    ReopenConsoleStreams();
            }

            return HasInheritedConsole();
        }

        public static bool HasInheritedConsole()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return true;

            var processList = new uint[8];
            uint processCount = GetConsoleProcessList(processList, (uint)processList.Length);
            return HasInheritedConsoleProcessCount(processCount);
        }

        static bool HasInheritedConsoleProcessCount(uint processCount)
        {
            if (processCount == 1)
                return false;

            if (processCount > 1)
                return true;

            // GetConsoleProcessList returns 0 on failure. In that ambiguous case, prefer the
            // shell/double-click behavior for no-argument startup instead of showing CLI help.
            return false;
        }

        public static void HideForGui()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            if (!attachedParentConsole)
            {
                IntPtr consoleWindow = GetConsoleWindow();
                if (consoleWindow != IntPtr.Zero)
                    ShowWindow(consoleWindow, SW_HIDE);
            }

            FreeConsole();
        }

        private static void ReopenConsoleStreams()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    var output = new StreamWriter(Console.OpenStandardOutput(), CreateConsoleStreamEncoding(Console.OutputEncoding)) { AutoFlush = true };
                    Console.SetOut(output);
                }

                if (!Console.IsErrorRedirected)
                {
                    var error = new StreamWriter(Console.OpenStandardError(), CreateConsoleStreamEncoding(Console.Error.Encoding)) { AutoFlush = true };
                    Console.SetError(error);
                }
            }
            catch
            {
                // Console stream repair is best-effort; redirected CLI tests and GUI startup
                // should continue even when a host denies stream reopening.
            }
        }

        private static Encoding CreateConsoleStreamEncoding(Encoding encoding)
        {
            if (encoding.CodePage == Encoding.UTF8.CodePage)
            {
                var utf8 = (Encoding)new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).Clone();
                utf8.EncoderFallback = encoding.EncoderFallback;
                utf8.DecoderFallback = encoding.DecoderFallback;
                return utf8;
            }

            return encoding;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList([Out] uint[] processList, uint processCount);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
