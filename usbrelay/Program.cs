//  
//  USB-Relay Utility
//      - A generic tool to handle common 1-8 channel USB-Relay boards.
//      - Developd for Astro-Photograpy Equipment Power Control.
//
//  Author: Min Xie (minxie.dallas@gmail.com)
//

using System;
using System.Windows.Forms;

namespace usbrelay
{
    internal class Program
    {
        enum StartupMode { Cli, Gui };

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--apply-update")
                return UpdateLauncher.Apply(args);
            // Keep inherited pipe handles intact: an MCP host launches this WinExe with
            // redirected stdio, and attaching a console can replace those handles.
            if (args.Length > 0 && args[0] == "mcp")
                return UsbRelayCli.Run(args);
            bool hasInheritedConsole = ConsoleWindow.PrepareForStartup();
            if (SelectStartupMode(args, hasInheritedConsole) == StartupMode.Gui)
                return RunGui();

            return UsbRelayCli.Run(args);
        }

        static StartupMode SelectStartupMode(string[] args, bool hasInheritedConsole)
        {
            ParsedCliCommand command = UsbRelayCli.ParseCommand(args);
            if (command.IsValid && command.IsGui && !command.HasRelayOptions)
                return StartupMode.Gui;

            if (args.Length == 0 && !hasInheritedConsole)
                return StartupMode.Gui;

            return StartupMode.Cli;
        }

        static int RunGui()
        {
            ConsoleWindow.HideForGui();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm mainForm;
            using (var splash = new SplashForm())
            {
                splash.Show();
                splash.SetStatus("Discovering USB-Relay devices...");
                splash.Refresh();
                Application.DoEvents();

                mainForm = new MainForm();
                mainForm.PrepareForDisplay();

                mainForm.Shown += (sender, args) => splash.Close();
                Application.Run(mainForm);
            }
            return 0;
        }
    }
}
