using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace usbrelay.Tests
{
    internal static class UpdateFeatureTests
    {
        private static readonly byte[] Installer = Encoding.UTF8.GetBytes("test installer payload");
        private static readonly string Digest = Hash(Installer);

        public static void Run()
        {
            var tests = new Action[] { ReleaseSelectsVersionMatchedInstaller, ReleaseRejectsDraftsPrereleasesAndOlderVersions,
                ReleaseRejectsUntrustedAssets, DownloadChecksSizeAndDigest, DamagedDownloadsAreRemoved,
                CancelledDownloadDoesNotLeaveInstaller, CheckHandlesNoReleaseAndHttpErrors,
                SettingsPersistAndThrottleChecks, InstallerKeepsArbitraryPaths, UpdateMenuIsAvailable,
                ClosingWindowCancelsUpdates, VerifiedInstallerCannotBeReplaced,
                ManualUpdateResumesWithAutomaticChecksOff, ClosingDuringCheckIgnoresLateResponse };
            foreach (Action test in tests) { test(); Console.WriteLine("PASS " + test.Method.Name); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fails(Action action)
        {
            try { action(); }
            catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Expected invalid update data to be rejected.");
        }

        private static string Json(string tag = "v2.0.0.15", string extra = "", string digest = null,
            string url = null, string name = null, int? size = null)
        {
            return "{\"tag_name\":\"" + tag + "\",\"draft\":false,\"prerelease\":false," + extra
                + "\"assets\":[{\"name\":\"" + (name ?? "usbrelay-setup-" + tag + ".exe")
                + "\",\"size\":" + (size ?? Installer.Length) + ",\"digest\":\"sha256:" + (digest ?? Digest)
                + "\",\"browser_download_url\":\"" + (url ?? "https://github.com/dm17ryk/usbrelay/releases/download/" + tag + "/usbrelay-setup-" + tag + ".exe") + "\"}]}";
        }

        private static AvailableUpdate Parse(string json) => GitHubUpdateService.ParseRelease(json, new Version(1, 0, 0, 14));
        private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "usbrelay-update-tests-" + Guid.NewGuid().ToString("N"));
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void ReleaseSelectsVersionMatchedInstaller()
        {
            var update = Parse(Json());
            Check(update.Version == new Version(2, 0, 0, 15), "Release version must be parsed numerically.");
            Check(update.AssetName == "usbrelay-setup-v2.0.0.15.exe" && update.Sha256 == Digest, "Must select the matching installer and digest.");
            Check(Parse(Json("v1.0.0.100")).Version.Revision == 100, "Revision ordering must support four-part versions.");
        }

        private static void ReleaseRejectsDraftsPrereleasesAndOlderVersions()
        {
            Check(Parse(Json().Replace("\"draft\":false", "\"draft\":true")) == null, "Drafts must be ignored.");
            Check(Parse(Json().Replace("\"prerelease\":false", "\"prerelease\":true")) == null, "Prereleases must be ignored.");
            Check(Parse(Json("v1.0.0.14")) == null && Parse(Json("v1.0.0.9")) == null, "Current and older versions must not install.");
            Check(Parse(Json("v1.0.0")) == null, "Three-part version must normalize to revision zero.");
            Fails(() => Parse(Json("main")));
        }

        private static void ReleaseRejectsUntrustedAssets()
        {
            Fails(() => Parse(Json(url: "http://github.com/dm17ryk/usbrelay/setup.exe")));
            Fails(() => Parse(Json(url: "https://evil.example/setup.exe")));
            Fails(() => Parse(Json(url: "https://github.com/other/repo/setup.exe")));
            Fails(() => Parse(Json(digest: "bad")));
            Fails(() => Parse(Json(name: "portable.zip")));
            Fails(() => Parse(Json(size: 0)));
            Fails(() => Parse(Json(size: 300 * 1024 * 1024)));
        }

        private static void DownloadChecksSizeAndDigest()
        {
            string root = TempDirectory();
            try
            {
                using (var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer) }))
                using (var service = new GitHubUpdateService(handler))
                {
                    string path = service.DownloadAsync(Parse(Json()), root, CancellationToken.None).GetAwaiter().GetResult();
                    Check(File.Exists(path) && File.ReadAllBytes(path).Length == Installer.Length, "Verified installer must be retained.");
                    GitHubUpdateService.VerifyInstaller(path, Digest);
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void DamagedDownloadsAreRemoved()
        {
            foreach (byte[] payload in new[] { Encoding.UTF8.GetBytes("short"), Encoding.UTF8.GetBytes("test installer payloae") })
            {
                string root = TempDirectory();
                try
                {
                    using (var service = new GitHubUpdateService(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) })))
                        Fails(() => service.DownloadAsync(Parse(Json()), root, CancellationToken.None).GetAwaiter().GetResult());
                    Check(Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories).Length == 0, "Rejected installer must be removed.");
                }
                finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            }
        }

        private static void CancelledDownloadDoesNotLeaveInstaller()
        {
            string root = TempDirectory();
            try
            {
                using (var cancellation = new CancellationTokenSource())
                using (var service = new GitHubUpdateService(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer) })))
                {
                    cancellation.Cancel();
                    try { service.DownloadAsync(Parse(Json()), root, cancellation.Token).GetAwaiter().GetResult(); throw new InvalidOperationException("Expected cancellation."); }
                    catch (OperationCanceledException) { }
                    Check(Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories).Length == 0, "Cancellation must not leave an installer.");
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void CheckHandlesNoReleaseAndHttpErrors()
        {
            using (var service = new GitHubUpdateService(new Handler(request =>
            {
                Check(request.RequestUri.AbsoluteUri == GitHubUpdateService.ReleasesApi, "Must use latest stable release endpoint.");
                Check(request.Headers.UserAgent.Count > 0, "GitHub requires a User-Agent.");
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }))) Check(service.CheckAsync(CancellationToken.None).GetAwaiter().GetResult() == null, "No release must be handled.");
            using (var service = new GitHubUpdateService(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))))
            {
                try { service.CheckAsync(CancellationToken.None).GetAwaiter().GetResult(); throw new InvalidOperationException("HTTP errors must propagate."); }
                catch (HttpRequestException) { }
            }
        }

        private static void SettingsPersistAndThrottleChecks()
        {
            string root = TempDirectory();
            string path = Path.Combine(root, "updates.json");
            try
            {
                DateTime now = DateTime.UtcNow;
                var settings = UpdateSettings.Load(path);
                Check(settings.AutomaticChecks && settings.IsCheckDue(now), "New installation must check automatically.");
                settings.LastCheckUtc = now;
                settings.Save(path);
                settings = UpdateSettings.Load(path);
                Check(!settings.IsCheckDue(now.AddHours(23)) && settings.IsCheckDue(now.AddHours(24)), "Checks must be throttled to one day.");
                settings.AutomaticChecks = false;
                settings.Save(path);
                Check(!UpdateSettings.Load(path).IsCheckDue(now.AddDays(2)), "Disabled setting must persist.");
                File.WriteAllText(path, "invalid json");
                Check(UpdateSettings.Load(path).AutomaticChecks, "Corrupt settings must fall back safely.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void InstallerKeepsArbitraryPaths()
        {
            Check(UpdateLauncher.InstallerArguments(@"C:\Essence_SC\usbrelay") == @"/S /D=C:\Essence_SC\usbrelay", "Must keep the current custom folder.");
            Check(UpdateLauncher.InstallerArguments(@"D:\Custom apps\USB Relay") == @"/S /D=D:\Custom apps\USB Relay", "NSIS /D must be last and unquoted for paths with spaces.");
            Check(UpdateLauncher.InstallerArguments(@"D:\USB Relay שלום") == @"/S /D=D:\USB Relay שלום", "Unicode folders must be preserved.");
        }

        private static void UpdateMenuIsAvailable()
        {
            using (var form = new MainForm(new FakeRelayBackend(), new usbrelay.Sequences.SequenceRepository(Path.Combine(TempDirectory(), "sequences.json"))))
            {
                Check(form.MainMenuStrip.Items.Find("checkUpdatesMenuItem", true).Length == 1, "Help must expose manual update check.");
                Check(form.MainMenuStrip.Items.Find("automaticUpdatesMenuItem", true).Length == 1, "Help must expose automatic check preference.");
            }
        }

        private static void ClosingWindowCancelsUpdates()
        {
            using (var form = new MainForm(new FakeRelayBackend(), new usbrelay.Sequences.SequenceRepository(Path.Combine(TempDirectory(), "sequences.json"))))
            {
                form.Dispose();
                var field = typeof(MainForm).GetField("updateCancellation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Check(((CancellationTokenSource)field.GetValue(form)).IsCancellationRequested, "Disposing must cancel pending network operations.");
            }
        }

        private static void VerifiedInstallerCannotBeReplaced()
        {
            string root = TempDirectory();
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "setup.exe");
            try
            {
                File.WriteAllBytes(path, Installer);
                using (var verified = UpdateLauncher.OpenVerifiedInstaller(path, Digest))
                {
                    try { File.WriteAllBytes(path, new byte[] { 1 }); throw new InvalidOperationException("Verified installer must reject replacement."); }
                    catch (IOException) { }
                    try { File.Delete(path); throw new InvalidOperationException("Verified installer must reject deletion."); }
                    catch (IOException) { }
                }
                Fails(() => { using (UpdateLauncher.OpenVerifiedInstaller(path, new string('0', 64))) { } });
                File.WriteAllBytes(path, Installer);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static Task CheckUpdates(MainForm form, bool manual)
        {
            return (Task)typeof(MainForm).GetMethod("CheckForUpdatesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(form, new object[] { manual });
        }

        private static void Pump(Task task)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
            Check(task.IsCompleted, "Update operation did not complete.");
            task.GetAwaiter().GetResult();
        }

        private static void ManualUpdateResumesWithAutomaticChecksOff()
        {
            string root = TempDirectory();
            string settingsPath = Path.Combine(root, "updates.json");
            try
            {
                new UpdateSettings { AutomaticChecks = false }.Save(settingsPath);
                var service = new FakeUpdateService { Result = Task.FromResult(Parse(Json())) };
                int confirmations = 0;
                using (var form = new MainForm(new FakeRelayBackend(), new usbrelay.Sequences.SequenceRepository(Path.Combine(root, "sequences.json")),
                    null, null, null, Path.Combine(root, "theme.json"), false, settingsPath, () => service,
                    update => { confirmations++; return false; }))
                {
                    form.PrepareForDisplay();
                    var field = typeof(MainForm).GetField("runningSequences", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var running = (HashSet<usbrelay.Sequences.SequenceDefinition>)field.GetValue(form);
                    var sequence = new usbrelay.Sequences.SequenceDefinition { Name = "Running" };
                    running.Add(sequence);
                    Pump(CheckUpdates(form, true));
                    Check(confirmations == 0, "Must not prompt to close while a sequence is running.");
                    running.Remove(sequence);
                    Pump(CheckUpdates(form, false));
                    Check(confirmations == 1, "A manually requested deferred update must resume even with automatic checks off.");
                    Check(service.CheckCount == 1 && service.DownloadCount == 0, "Resuming must reuse the checked release and honour postponement.");
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void ClosingDuringCheckIgnoresLateResponse()
        {
            string root = TempDirectory();
            try
            {
                var source = new TaskCompletionSource<AvailableUpdate>();
                var service = new FakeUpdateService { Result = source.Task };
                int confirmations = 0;
                using (var form = new MainForm(new FakeRelayBackend(), new usbrelay.Sequences.SequenceRepository(Path.Combine(root, "sequences.json")),
                    null, null, null, Path.Combine(root, "theme.json"), false, Path.Combine(root, "updates.json"), () => service,
                    update => { confirmations++; return false; }))
                {
                    form.PrepareForDisplay();
                    Task checking = CheckUpdates(form, true);
                    Check(service.CheckCount == 1 && !checking.IsCompleted, "Must have an in-flight check.");
                    form.Dispose();
                    source.SetResult(Parse(Json()));
                    Pump(checking);
                    Check(confirmations == 0 && service.DownloadCount == 0, "Late network results must not access disposed GUI or start installation.");
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private sealed class FakeUpdateService : IUpdateService
        {
            public Task<AvailableUpdate> Result { get; set; }
            public int CheckCount { get; private set; }
            public int DownloadCount { get; private set; }
            public Task<AvailableUpdate> CheckAsync(CancellationToken token) { CheckCount++; return Result; }
            public Task<string> DownloadAsync(AvailableUpdate update, string root, CancellationToken token)
            {
                DownloadCount++;
                throw new InvalidOperationException("No download was expected.");
            }
            public void Dispose() { }
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
            public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) { this.respond = respond; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(respond(request));
            }
        }
    }
}
