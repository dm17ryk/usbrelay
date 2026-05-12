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
            if (processCount == 0)
                return true;

            return processCount > 1;
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
