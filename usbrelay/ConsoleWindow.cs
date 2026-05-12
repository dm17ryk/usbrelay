using System;
using System.Runtime.InteropServices;

namespace usbrelay
{
    internal static class ConsoleWindow
    {
        private const int SW_HIDE = 0;

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

            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
                ShowWindow(consoleWindow, SW_HIDE);

            FreeConsole();
        }

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
