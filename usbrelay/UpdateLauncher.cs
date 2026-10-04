using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace usbrelay
{
    public static class UpdateLauncher
    {
        public static string InstallerArguments(string installationDirectory)
        {
            if (string.IsNullOrWhiteSpace(installationDirectory) || !Path.IsPathRooted(installationDirectory) ||
                installationDirectory.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("A valid absolute installation folder is required.");
            // NSIS requires /D= to be last and unquoted, including paths with spaces.
            return "/S /D=" + Path.GetFullPath(installationDirectory);
        }

        public static void Start(string installerPath, string sha256, string executablePath)
        {
            GitHubUpdateService.VerifyInstaller(installerPath, sha256);
            string stagingDirectory = Path.GetDirectoryName(installerPath);
            string helperPath = Path.Combine(stagingDirectory, "usbrelay-update-helper.exe");
            File.Copy(executablePath, helperPath, true);
            if (File.Exists(executablePath + ".config")) File.Copy(executablePath + ".config", helperPath + ".config", true);
            string arguments = "--apply-update " + Process.GetCurrentProcess().Id + " " + Quote(executablePath)
                + " " + Quote(installerPath) + " " + sha256;
            Trace.WriteLine("[Updater] Starting helper=" + helperPath + ", target=" + executablePath);
            using (var process = Process.Start(new ProcessStartInfo(helperPath, arguments) { UseShellExecute = false }))
            {
                if (process == null) throw new InvalidOperationException("The update helper could not start.");
            }
        }

        public static int Apply(string[] args)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.log");
            string target = null;
            bool parentExited = false;
            FileStream installerLock = null;
            try
            {
                if (args.Length != 5) throw new ArgumentException("Invalid update helper arguments.");
                int parentId;
                if (!int.TryParse(args[1], out parentId) || parentId <= 0) throw new ArgumentException("Invalid parent process.");
                target = Path.GetFullPath(args[2]);
                string installer = Path.GetFullPath(args[3]);
                if (!string.Equals(Path.GetFileName(target), "usbrelay.exe", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetDirectoryName(installer), AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Invalid staged installer or application path.");
                installerLock = OpenVerifiedInstaller(installer, args[4]);
                Log(logPath, "Waiting for application PID=" + parentId + ", target=" + target);
                Process parent = null;
                try { parent = Process.GetProcessById(parentId); }
                catch (ArgumentException) { Log(logPath, "Parent already exited."); }
                if (parent != null)
                {
                    using (parent)
                    {
                        try
                        {
                            if (!parent.HasExited && !string.Equals(parent.MainModule.FileName, target, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("The parent process is not the application being updated.");
                        }
                        catch (InvalidOperationException) when (parent.HasExited)
                        {
                            Log(logPath, "Parent exited during process validation.");
                        }
                        catch (Win32Exception) when (parent.HasExited)
                        {
                            Log(logPath, "Parent exited during native module enumeration.");
                        }
                        if (!parent.WaitForExit(120000)) throw new TimeoutException("USB Relay Control did not close; update cancelled.");
                    }
                }
                parentExited = true;
                string installationDirectory = Path.GetDirectoryName(target);
                string arguments = InstallerArguments(installationDirectory);
                Log(logPath, "Launching installer with UAC: " + installer + " " + arguments);
                using (var setup = Process.Start(new ProcessStartInfo(installer, arguments)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(installer)
                }))
                {
                    if (setup == null) throw new InvalidOperationException("The installer could not start.");
                    setup.WaitForExit();
                    Log(logPath, "Installer exit code=" + setup.ExitCode);
                    if (setup.ExitCode != 0) throw new InvalidOperationException("The installer failed with exit code " + setup.ExitCode + ".");
                }
                Log(logPath, "Update installed in " + installationDirectory);
                return 0;
            }
            catch (Exception ex)
            {
                Log(logPath, "Update failed: " + ex);
                MessageBox.Show("The update could not be installed.\n\n" + ex.Message + "\n\nLog: " + logPath,
                    "USB Relay update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                installerLock?.Dispose();
                if (parentExited && target != null && File.Exists(target))
                {
                    try
                    {
                        Log(logPath, "Restarting application as the current user: " + target);
                        Restart(target);
                    }
                    catch (Exception ex)
                    {
                        Log(logPath, "Restart failed: " + ex);
                        MessageBox.Show("Please start USB Relay Control manually.\n\n" + ex.Message, "USB Relay update");
                    }
                }
            }
        }

        public static FileStream OpenVerifiedInstaller(string path, string sha256)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                GitHubUpdateService.VerifyInstaller(stream, sha256);
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        private static void Restart(string target)
        {
            bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            Trace.WriteLine("[Updater] Restart elevation=" + elevated + "; using " + (elevated ? "interactive Explorer shell" : "current user token"));
            if (!elevated)
            {
                using (var application = Process.Start(new ProcessStartInfo(target, "--gui")
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(target)
                })) { }
                return;
            }
            // Obtain the shell object hosted by the interactive user's Explorer,
            // rather than constructing an elevated Shell.Application instance.
            object windows = null;
            object desktop = null;
            object document = null;
            object shell = null;
            try
            {
                windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                dynamic shellWindows = windows;
                object location = 0;
                object root = 0;
                int hwnd;
                desktop = shellWindows.FindWindowSW(ref location, ref root, 8, out hwnd, 1);
                if (desktop == null) throw new InvalidOperationException("The interactive Explorer desktop is unavailable.");
                document = ((dynamic)desktop).Document;
                shell = ((dynamic)document).Application;
                ((dynamic)shell).ShellExecute(target, "--gui", Path.GetDirectoryName(target), "open", 1);
            }
            finally
            {
                foreach (object item in new[] { shell, document, desktop, windows })
                    if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item);
            }
        }

        private static string Quote(string path)
        {
            if (path.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0) throw new ArgumentException("Invalid update path.");
            return "\"" + path + "\"";
        }

        private static void Log(string path, string message)
        {
            Trace.WriteLine("[Updater] " + message);
            try { File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine); }
            catch (Exception ex) { Trace.WriteLine("[Updater] Log write failed: " + ex); }
        }
    }
}
