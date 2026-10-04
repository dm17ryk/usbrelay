using System;
using System.Threading;

namespace usbrelay
{
    public static class UpdateCommand
    {
        public static int Run(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[1] == "check")
                {
                    using (var service = new GitHubUpdateService())
                    {
                        var update = service.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
                        Console.WriteLine(update == null ? "USB Relay Control is up to date (" + GitHubUpdateService.CurrentVersion + ")."
                            : "Update available: " + update.Version + Environment.NewLine + update.DownloadUrl);
                        return 0;
                    }
                }
                if (args.Length == 3 && args[1] == "auto" && (args[2] == "on" || args[2] == "off"))
                {
                    var settings = UpdateSettings.Load(UpdateSettings.DefaultPath);
                    settings.AutomaticChecks = args[2] == "on";
                    settings.Save(UpdateSettings.DefaultPath);
                    Console.WriteLine("Automatic update checks " + (settings.AutomaticChecks ? "enabled" : "disabled"));
                    return 0;
                }
                Console.Error.WriteLine("Usage: usbrelay update check | usbrelay update auto on|off");
                return 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[Updater] CLI update command failed: " + ex);
                Console.Error.WriteLine("Update failed: " + ex.Message);
                return 1;
            }
        }
    }
}
