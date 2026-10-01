using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using usbrelay.Sequences;

namespace usbrelay.Tests
{
    internal static class IntegrationFeatureTests
    {
        public static void Run()
        {
            var tests = new Action[] { CliRemovalAndAllOff, NamesCanBeCleared, McpToolsShareRepositoryAndReportErrors, SequenceCancellationStopsLaterActions, ThemeRoundTripAndControls, McpExecutableInteroperatesWithOfficialClient };
            foreach (Action test in tests)
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }
        }

        private static string TempPath(string file) => Path.Combine(Path.GetTempPath(), "usbrelay-feature-tests-" + Guid.NewGuid().ToString("N"), file);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void CliRemovalAndAllOff()
        {
            var repository = new SequenceRepository(TempPath("sequences.json"));
            var backend = new FakeRelayBackend(new RelayDevice("ONE", RelayDeviceType.TwoChannel, 2, 3), new RelayDevice("TWO", RelayDeviceType.TwoChannel, 2, 3));
            var cli = new SequenceCli(repository, backend, new FakeExternalToolRunner(""), new StringWriter(), new StringWriter());
            Check(cli.Add("Keep", "sequence.Sleep(0);", null, "Run", "") == 0, "Add sequence failed");
            Check(cli.Add("Delete", "sequence.Sleep(0);", null, "Run", "") == 0, "Add second sequence failed");
            Check(cli.Remove("delete") == 0, "Case-insensitive remove failed");
            Check(repository.Load().Single().Name == "Keep", "Remove must preserve other sequences");
            Check(cli.Remove("missing") == 1 && cli.Remove(null) == 1, "Remove must reject missing name and missing sequence");
            repository.Save(new[] { new SequenceDefinition { Name = "Duplicate" }, new SequenceDefinition { Name = "duplicate" } });
            Check(cli.Remove("Duplicate") == 1 && repository.Load().Count == 2, "Ambiguous removal must preserve repository");
            var service = new RelayService(backend, new RelayNamingRepository(TempPath("names.json")));
            Check(service.AllOff().Count == 0, "All Off failed");
            Check(backend.EnumerateDevices().All(device => device.StatusMask == 0), "All Off must switch every channel on every board");
        }

        private static void NamesCanBeCleared()
        {
            var backend = new FakeRelayBackend(new RelayDevice("NAME", RelayDeviceType.TwoChannel, 2, 0, "test-device"));
            var service = new RelayService(backend, new RelayNamingRepository(TempPath("names.json")));
            service.UpdateNames("NAME", "Device", new[] { "1", "Channel" });
            Check(service.GetDevice("Device").GetChannelName(1) == "Channel", "Initial naming failed");
            service.UpdateNames("Device", "", new[] { "1", "" });
            Check(service.GetDevice("NAME").DeviceName == "" && service.GetDevice("NAME").GetChannelName(1) == "CH1", "Empty names must clear device/channel names");
        }

        private static void McpToolsShareRepositoryAndReportErrors()
        {
            var backend = new FakeRelayBackend(new RelayDevice("MCP", RelayDeviceType.TwoChannel, 2, 0, "mcp-test-device"));
            var repository = new SequenceRepository(TempPath("sequences.json"));
            var service = new RelayService(backend, new RelayNamingRepository(TempPath("names.json")));
            using (var tools = new UsbRelayMcpTools(service, repository, backend, new StringWriter(), TempPath("theme.json")))
            {
                Check(!tools.AddSequence("Test", "sequence.PowerOn(\"MCP\", 1);").GetAwaiter().GetResult().IsError.GetValueOrDefault(), "MCP add failed");
                Check(repository.Load().Single().Name == "Test", "MCP must use shared repository");
                Check(!tools.ModifySequence("Test", description: "Changed").GetAwaiter().GetResult().IsError.GetValueOrDefault(), "MCP modify failed");
                Check(repository.Load().Single().Description == "Changed", "MCP edit did not persist");
                Check(!tools.RunSequence("Test").GetAwaiter().GetResult().IsError.GetValueOrDefault(), "MCP sequence run failed");
                Check(backend.GetDevice("MCP").IsChannelOn(1), "MCP sequence did not switch channel");
                Check(tools.SetChannel("MCP", "3", true).GetAwaiter().GetResult().IsError.GetValueOrDefault(), "Invalid channel must return isError");
                Check(!tools.AllOff().GetAwaiter().GetResult().IsError.GetValueOrDefault(), "MCP All Off failed");
                Check(backend.GetDevice("MCP").StatusMask == 0, "MCP All Off did not switch channel");
                Check(!tools.RemoveSequence("Test").GetAwaiter().GetResult().IsError.GetValueOrDefault() && repository.Load().Count == 0, "MCP remove failed");
                Check(tools.ReadSequence("missing").GetAwaiter().GetResult().IsError.GetValueOrDefault(), "Missing sequence must be a tool error");
                Check(tools.Theme("invalid").GetAwaiter().GetResult().IsError.GetValueOrDefault(), "Invalid theme must be a tool error");
                Check(!tools.Theme("light").GetAwaiter().GetResult().IsError.GetValueOrDefault(), "MCP theme failed");
                repository.Save(new[] { new SequenceDefinition { Name = "Cancelled", Script = "var ok = sequence.Confirm(\"Title\", \"Message\");\nif (!ok) {\nsequence.Exit(\"Cancelled\");\n}\nsequence.PowerOn(\"MCP\", 1);" } });
                Check(!tools.RunSequence("Cancelled", confirm: false).GetAwaiter().GetResult().IsError.GetValueOrDefault(), "Confirm false should follow successful Exit branch");
                Check(!backend.GetDevice("MCP").IsChannelOn(1), "Confirm false must skip relay action after Exit");
            }
        }

        private static void SequenceCancellationStopsLaterActions()
        {
            var backend = new FakeRelayBackend(new RelayDevice("CANCEL", RelayDeviceType.TwoChannel, 2, 0));
            var parsed = SequenceParser.Parse("sequence.Sleep(10000);\nsequence.PowerOn(\"CANCEL\", 1);");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(100);
                var watch = Stopwatch.StartNew();
                var result = SequenceRunner.Run(parsed, backend, new FakeExternalToolRunner(""), skipDelays: false, cancellationToken: cancellation.Token);
                Check(!result.Success && result.Error is OperationCanceledException, "Sequence must report cancellation");
                Check(watch.ElapsedMilliseconds < 2000 && !backend.GetDevice("CANCEL").IsChannelOn(1), "Cancellation must interrupt delay and skip later relay action");
            }
        }

        private static void ThemeRoundTripAndControls()
        {
            string path = TempPath("theme.json");
            Check(ThemeSettings.Load(path).Theme == "dark", "New installations must default to dark");
            new ThemeSettings { Theme = "Light" }.Save(path);
            Check(ThemeSettings.Load(path).Theme == "light", "Theme preference must round-trip");
            using (var form = new MainForm(new FakeRelayBackend(), new SequenceRepository(TempPath("sequences.json"))))
            {
                var dark = new GuiTheme("dark");
                dark.Apply(form);
                Check(form.BackColor == dark.Background && form.ForeColor == dark.Foreground, "Main form dark palette failed");
                var light = new GuiTheme("light");
                light.Apply(form);
                Check(form.BackColor == light.Background, "Light theme switching failed");
            }
        }

        private static void McpExecutableInteroperatesWithOfficialClient()
        {
            RunClientTest().GetAwaiter().GetResult();
        }

        private static async Task RunClientTest()
        {
            string executable = TempPath("usbrelay.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executable));
            string builtExecutable = typeof(MainForm).Assembly.Location;
            File.Copy(builtExecutable, executable);
            if (File.Exists(builtExecutable + ".config")) File.Copy(builtExecutable + ".config", executable + ".config");
            // Costura embeds the SDK in the application, so launching this actual
            // WinExe without adjacent SDK DLLs also tests dependency embedding.
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "usbrelay-integration-test",
                    Command = executable,
                    Arguments = new[] { "mcp" },
                    WorkingDirectory = Path.GetDirectoryName(executable)
                });
                McpClient client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token).ConfigureAwait(false);
                try
                {
                    var tools = await client.ListToolsAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
                    Check(tools.Count == 13, "Expected all 13 MCP tools");
                    Check(tools.Any(tool => tool.Name == "relay_set_channel") && tools.Any(tool => tool.Name == "sequence_remove"), "Missing relay/sequence tools");
                    var reference = await client.CallToolAsync("sequence_functions", cancellationToken: timeout.Token).ConfigureAwait(false);
                    Check(!reference.IsError.GetValueOrDefault() && reference.Content.OfType<TextContentBlock>().Single().Text.Contains("PowerOn"), "MCP tools/call interoperability failed");
                    var devices = await client.CallToolAsync("relay_list", cancellationToken: timeout.Token).ConfigureAwait(false);
                    Check(!devices.IsError.GetValueOrDefault(), "Standalone MCP native relay enumeration failed");
                    var error = await client.CallToolAsync("relay_set_channel", new Dictionary<string, object> { ["device"] = "", ["channel"] = "1", ["on"] = true }, cancellationToken: timeout.Token).ConfigureAwait(false);
                    Check(error.IsError.GetValueOrDefault(), "MCP validation must return tool error without hardware changes");
                }
                finally { await client.DisposeAsync().ConfigureAwait(false); }
            }
        }
    }
}
