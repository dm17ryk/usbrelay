using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using usbrelay.Sequences;

namespace usbrelay.Tests
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            if (Environment.GetCommandLineArgs().Contains("--write-large-stderr"))
            {
                Console.Error.Write(new string('e', 256 * 1024));
                Console.Out.WriteLine("stdout-done");
                return 0;
            }

            var tests = new Action[]
            {
                SequenceRepository_RoundTripsSequencesAsJson,
                SequenceParser_ParsesDslAndResources,
                SequenceParseCache_ReusesParseUntilScriptChanges,
                SequenceResourceLocks_BlockOverlappingChannelsOnly,
                SequenceRunner_ExecutesRegexSuccessBranchWithFakeRelayAndTool,
                ProcessExternalToolRunner_DoesNotDeadlockWhenStderrPipeFills,
                Program_SelectStartupMode_UsesCliForTerminalWithoutArguments,
                Program_SelectStartupMode_UsesGuiForNonTerminalWithoutArguments,
                Program_SelectStartupMode_UsesGuiForLongGuiArgument,
                Program_SelectStartupMode_UsesGuiForLegacyGuiArgument,
                Program_SelectStartupMode_UsesCliForCliArguments,
                Program_HasInheritedConsoleProcessCount_UsesGuiSafeFallbackForFailure,
                Program_ParseCliCommand_RecognizesHelpAliases,
                Program_ParseCliCommand_RecognizesVersionAliases,
                Program_ParseCliCommand_MapsLegacyRelayOptions,
                Program_ParseCliCommand_ParsesMultiValueChannels,
                Program_ParseCliCommand_RejectsGuiWithRelayOptions,
                Program_CompletionSuggestsTopLevelOptions,
                Program_CompletionSuggestsMatchingOptions,
                Program_CompletionSuggestsOnChannels,
                Program_CompletionSuggestsOffChannels,
                Program_CompletionSuggestsLegacyAliases,
                Program_CompletionHandlesQuotedExecutablePath,
                Program_HelpDoesNotShowCompletionCommand,
                Program_NoArgumentTerminalRunPrintsUsageAndExits,
                Program_HelpArgumentPrintsHelpAndExits,
                Program_HelpArgumentPrintsExamples,
                Program_VersionArgumentPrintsVersionAndExits,
                Program_AssemblyVersionMatchesVersionProps,
                MainForm_LoadsSavedSequencesIntoVisibleRows,
                MainForm_RunButtonClickExecutesVisibleSequence,
                MainForm_AllOffRefreshesDevicesOnceAfterChannelUpdates,
                MainLayoutSettings_RoundTripsWindowAndPaneSizes,
                MainForm_SavesLayoutSettings,
                SequenceEditorLayoutSettings_RoundTripsWindowAndSplitter,
                SequenceEditorForm_SavesLayoutSettings
            };

            foreach (var test in tests)
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }

            return 0;
        }

        private static void SequenceRepository_RoundTripsSequencesAsJson()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            var original = new SequenceDefinition
            {
                Name = "Power cycle DUT",
                RunButtonText = "Run",
                Description = "Cycle DUT power",
                Script = "sequence.PowerOff(\"6QMBS\", 1);"
            };

            repository.Save(new[] { original });
            var loaded = repository.Load().Single();

            AssertEqual(original.Name, loaded.Name, "Name");
            AssertEqual(original.RunButtonText, loaded.RunButtonText, "RunButtonText");
            AssertEqual(original.Description, loaded.Description, "Description");
            AssertEqual(original.Script, loaded.Script, "Script");
        }

        private static void SequenceParser_ParsesDslAndResources()
        {
            string script = string.Join(Environment.NewLine, new[]
            {
                "sequence.PowerOff(\"6QMBS\", 1);",
                "sequence.ReadChannel(\"6QMBS\", 1);",
                "sequence.Sleep(500);",
                "sequence.WaitChannel(\"6QMBS\", 1, RelayState.Off, 3000);"
            });

            var result = SequenceParser.Parse(script);

            AssertTrue(result.IsValid, "Script should validate");
            AssertEqual(4, result.Actions.Count, "Action count");
            AssertTrue(result.Resources.Contains(new RelayResource("6QMBS", 1)), "CH1 resource should be claimed");
        }

        private static void SequenceParseCache_ReusesParseUntilScriptChanges()
        {
            int parseCount = 0;
            var cache = new SequenceParseCache(script =>
            {
                parseCount++;
                return SequenceParser.Parse(script);
            });
            var sequence = new SequenceDefinition { Script = "sequence.PowerOff(\"6QMBS\", 1);" };

            var first = cache.Get(sequence);
            var second = cache.Get(sequence);

            AssertTrue(object.ReferenceEquals(first, second), "Parse result should be reused while script is unchanged");
            AssertEqual(1, parseCount, "Parser call count before script change");

            sequence.Script = "sequence.PowerOff(\"6QMBS\", 2);";
            var third = cache.Get(sequence);

            AssertFalse(object.ReferenceEquals(first, third), "Parse result should be replaced after script changes");
            AssertEqual(2, parseCount, "Parser call count after script change");
        }

        private static void SequenceResourceLocks_BlockOverlappingChannelsOnly()
        {
            var locks = new SequenceResourceLocks();
            var channel1 = new[] { new RelayResource("6QMBS", 1) };
            var channel2 = new[] { new RelayResource("6QMBS", 2) };

            AssertTrue(locks.TryReserve("first", channel1), "First reservation should succeed");
            AssertFalse(locks.TryReserve("conflict", channel1), "Overlapping reservation should fail");
            AssertTrue(locks.TryReserve("parallel", channel2), "Different channel should run in parallel");

            locks.Release("first");
            AssertTrue(locks.TryReserve("after-release", channel1), "Released channel should be available");
        }

        private static void SequenceRunner_ExecutesRegexSuccessBranchWithFakeRelayAndTool()
        {
            string script = string.Join(Environment.NewLine, new[]
            {
                "sequence.PowerOff(\"6QMBS\", 1);",
                "var tool = sequence.RunTool(\"tool.exe\", \"--probe\");",
                "if (tool.OutputMatches(\"READY|OK\")) {",
                "    sequence.PowerOn(\"6QMBS\", 2);",
                "} else {",
                "    sequence.PowerOff(\"6QMBS\", 2);",
                "}"
            });

            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            var tools = new FakeExternalToolRunner("READY");
            var result = SequenceRunner.Run(SequenceParser.Parse(script), relay, tools);

            AssertTrue(result.Success, "Sequence should succeed");
            AssertFalse(relay.GetChannelState("6QMBS", 1), "CH1 should be off");
            AssertTrue(relay.GetChannelState("6QMBS", 2), "CH2 should be on after success branch");
            AssertTrue(result.Log.Any(line => line.Contains("OutputMatches READY|OK: success")), "Regex match should be logged");
        }

        private static void ProcessExternalToolRunner_DoesNotDeadlockWhenStderrPipeFills()
        {
            DateTime startedAfter = DateTime.Now.AddSeconds(-1);
            var task = Task.Run(() => new ProcessExternalToolRunner().Run(Assembly.GetExecutingAssembly().Location, "--write-large-stderr"));

            if (!task.Wait(TimeSpan.FromSeconds(5)))
            {
                KillStuckTestChildren(startedAfter);
                throw new InvalidOperationException("External tool runner deadlocked while child wrote large stderr output");
            }

            AssertEqual(0, task.Result.ExitCode, "External tool exit code");
            AssertTrue(task.Result.Output.Contains("stdout-done"), "External tool stdout should be captured");
            AssertTrue(task.Result.Output.Length > 200000, "External tool stderr should be captured");
        }

        private static void Program_SelectStartupMode_UsesCliForTerminalWithoutArguments()
        {
            AssertEqual("Cli", SelectStartupMode(new string[0], true).ToString(), "Terminal no-argument mode");
        }

        private static void Program_SelectStartupMode_UsesGuiForNonTerminalWithoutArguments()
        {
            AssertEqual("Gui", SelectStartupMode(new string[0], false).ToString(), "Non-terminal no-argument mode");
        }

        private static void Program_SelectStartupMode_UsesGuiForLongGuiArgument()
        {
            AssertEqual("Gui", SelectStartupMode(new[] { "--gui" }, true).ToString(), "Long GUI argument mode");
        }

        private static void Program_SelectStartupMode_UsesGuiForLegacyGuiArgument()
        {
            AssertEqual("Gui", SelectStartupMode(new[] { "-gui" }, true).ToString(), "Legacy GUI argument mode");
        }

        private static void Program_SelectStartupMode_UsesCliForCliArguments()
        {
            AssertEqual("Cli", SelectStartupMode(new[] { "-list" }, true).ToString(), "CLI argument mode");
        }

        private static void Program_HasInheritedConsoleProcessCount_UsesGuiSafeFallbackForFailure()
        {
            AssertFalse(HasInheritedConsoleProcessCount(0), "Failed console process query should not force inherited-console mode");
            AssertFalse(HasInheritedConsoleProcessCount(1), "Single console process should be treated as standalone shell launch");
            AssertTrue(HasInheritedConsoleProcessCount(2), "Multiple console processes should be treated as inherited terminal launch");
        }

        private static void Program_ParseCliCommand_RecognizesHelpAliases()
        {
            AssertTrue(GetProperty<bool>(ParseCliCommand(new[] { "-h" }), "IsHelpRequested"), "-h should request help");
            AssertTrue(GetProperty<bool>(ParseCliCommand(new[] { "--help" }), "IsHelpRequested"), "--help should request help");
            AssertTrue(GetProperty<bool>(ParseCliCommand(new[] { "-?" }), "IsHelpRequested"), "-? should request help");
        }

        private static void Program_ParseCliCommand_RecognizesVersionAliases()
        {
            AssertTrue(GetProperty<bool>(ParseCliCommand(new[] { "-v" }), "IsVersionRequested"), "-v should request version");
            AssertTrue(GetProperty<bool>(ParseCliCommand(new[] { "--version" }), "IsVersionRequested"), "--version should request version");
        }

        private static void Program_ParseCliCommand_MapsLegacyRelayOptions()
        {
            object command = ParseCliCommand(new[] { "-serial", "BITFT", "-on", "1", "2", "-off", "3" });

            AssertEqual("BITFT", GetProperty<string>(command, "Serial"), "Serial option");
            AssertEqual("ONOFF", GetProperty<object>(command, "Operation").ToString(), "Relay operation");
            AssertSequence(new[] { 1, 2 }, GetProperty<IEnumerable<int>>(command, "OnChannels"), "On channels");
            AssertSequence(new[] { 3 }, GetProperty<IEnumerable<int>>(command, "OffChannels"), "Off channels");
        }

        private static void Program_ParseCliCommand_ParsesMultiValueChannels()
        {
            object command = ParseCliCommand(new[] { "--on", "1", "2", "3", "--off", "4", "5" });

            AssertEqual("ONOFF", GetProperty<object>(command, "Operation").ToString(), "Relay operation");
            AssertSequence(new[] { 1, 2, 3 }, GetProperty<IEnumerable<int>>(command, "OnChannels"), "On channels");
            AssertSequence(new[] { 4, 5 }, GetProperty<IEnumerable<int>>(command, "OffChannels"), "Off channels");
        }

        private static void Program_ParseCliCommand_RejectsGuiWithRelayOptions()
        {
            object command = ParseCliCommand(new[] { "--gui", "-list" });

            AssertFalse(GetProperty<bool>(command, "IsValid"), "GUI mixed with relay options should be invalid");
            AssertTrue(GetProperty<IEnumerable<string>>(command, "Errors").Any(error => error.Contains("--gui")), "GUI conflict error should mention --gui");
        }

        private static void Program_CompletionSuggestsTopLevelOptions()
        {
            string[] completions = RunCompletion("usbrelay --");

            AssertContains(completions, "--list", "Top-level completions should include --list");
            AssertContains(completions, "--status", "Top-level completions should include --status");
            AssertContains(completions, "--serial", "Top-level completions should include --serial");
            AssertContains(completions, "--gui", "Top-level completions should include --gui");
            AssertFalse(completions.Contains("list"), "Top-level completions should not include bare option names");
        }

        private static void Program_CompletionSuggestsMatchingOptions()
        {
            string[] completions = RunCompletion("usbrelay --s");

            AssertContains(completions, "--serial", "--s completions should include --serial");
            AssertContains(completions, "--status", "--s completions should include --status");
            AssertFalse(completions.Contains("--list"), "--s completions should not include --list");
        }

        private static void Program_CompletionSuggestsOnChannels()
        {
            string[] completions = RunCompletion("usbrelay --on ");

            AssertSequence(new[] { "1", "2", "3", "4", "5", "6", "7", "8" }, completions, "--on channel completions");
        }

        private static void Program_CompletionSuggestsOffChannels()
        {
            string[] completions = RunCompletion("usbrelay --serial BITFT --off ");

            AssertSequence(new[] { "1", "2", "3", "4", "5", "6", "7", "8" }, completions, "--off channel completions");
        }

        private static void Program_CompletionSuggestsLegacyAliases()
        {
            string[] completions = RunCompletion("usbrelay -");

            AssertContains(completions, "-list", "Legacy completions should include -list");
            AssertContains(completions, "-serial", "Legacy completions should include -serial");
            AssertContains(completions, "-on", "Legacy completions should include -on");
            AssertContains(completions, "-off", "Legacy completions should include -off");
            AssertContains(completions, "-gui", "Legacy completions should include -gui");
            AssertContains(completions, "-v", "Legacy completions should include -v");
        }

        private static void Program_CompletionHandlesQuotedExecutablePath()
        {
            string[] completions = RunCompletion("\"C:\\Program Files\\usbrelay.exe\" --s");

            AssertContains(completions, "--serial", "Quoted executable path completions should include --serial");
            AssertContains(completions, "--status", "Quoted executable path completions should include --status");
        }

        private static void Program_HelpDoesNotShowCompletionCommand()
        {
            ProcessResult result = RunUsbRelay("--help");

            AssertEqual(0, result.ExitCode, "--help exit code");
            AssertFalse(result.Output.Contains("complete"), "Hidden complete command should not be shown in help");
        }

        private static void Program_NoArgumentTerminalRunPrintsUsageAndExits()
        {
            ProcessResult result = RunUsbRelay();

            AssertEqual(0, result.ExitCode, "No-argument terminal exit code");
            AssertTrue(result.Output.Contains("Usage:"), "No-argument terminal run should print generated help");
            AssertTrue(result.Output.Contains("--gui"), "No-argument terminal usage should document --gui");
            AssertTrue(result.Output.Contains("Examples:"), "No-argument terminal usage should include examples");
            AssertEqual(string.Empty, result.Error, "No-argument terminal stderr");
        }

        private static void Program_HelpArgumentPrintsHelpAndExits()
        {
            ProcessResult result = RunUsbRelay("-h");

            AssertEqual(0, result.ExitCode, "-h exit code");
            AssertTrue(result.Output.Contains("Usage:"), "-h should print generated help");
            AssertTrue(result.Output.Contains("--version"), "Help should include version option");
            AssertEqual(string.Empty, result.Error, "-h stderr");
        }

        private static void Program_HelpArgumentPrintsExamples()
        {
            ProcessResult result = RunUsbRelay("--help");

            AssertEqual(0, result.ExitCode, "--help exit code");
            AssertTrue(result.Output.Contains("Examples:"), "--help should include examples");
            AssertTrue(result.Output.Contains("usbrelay --list"), "Help should include list example");
            AssertTrue(result.Output.Contains("usbrelay --serial BITFT --on 1 2 3"), "Help should include multi-channel on example");
            AssertTrue(result.Output.Contains("usbrelay --gui"), "Help should include GUI example");
        }

        private static void Program_VersionArgumentPrintsVersionAndExits()
        {
            ProcessResult result = RunUsbRelay("-v");
            string expectedVersion = typeof(MainForm).Assembly.GetName().Version.ToString();

            AssertEqual(0, result.ExitCode, "-v exit code");
            AssertTrue(result.Output.Contains(expectedVersion), "-v should print assembly version");
            AssertEqual(string.Empty, result.Error, "-v stderr");
        }

        private static void Program_AssemblyVersionMatchesVersionProps()
        {
            string versionPropsPath = FindRepoFile("Version.props");
            string expectedVersion = ReadXmlProperty(versionPropsPath, "UsbRelayVersion");
            string assemblyVersion = typeof(MainForm).Assembly.GetName().Version.ToString();

            AssertEqual(expectedVersion, assemblyVersion, "Assembly version should match Version.props");
        }

        private static void MainForm_LoadsSavedSequencesIntoVisibleRows()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Visible sequence",
                    RunButtonText = "Run",
                    Description = "tooltip text",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                }
            });

            using (var form = new MainForm(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                repository))
            {
                InvokePrivate(form, "LoadSequences");
                var sequenceList = (DataGridView)GetPrivateField(form, "sequenceGrid");

                AssertEqual(1, sequenceList.Rows.Count, "Visible sequence row count");
                AssertEqual("Visible sequence", sequenceList.Rows[0].Cells["NameColumn"].Value, "Sequence name should be visible");
                AssertEqual("Run", sequenceList.Rows[0].Cells["RunColumn"].Value, "Sequence run button should be visible");
            }
        }

        private static void MainForm_RunButtonClickExecutesVisibleSequence()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Turn CH1 on",
                    RunButtonText = "Run",
                    Description = "Turns channel one on",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                }
            });

            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            using (var form = new MainForm(relay, repository))
            {
                InvokePrivate(form, "LoadSequences");
                var sequenceList = (DataGridView)GetPrivateField(form, "sequenceGrid");
                int runColumnIndex = sequenceList.Columns["RunColumn"].Index;

                InvokePrivate(form, "SequenceGrid_CellClick", sequenceList, new DataGridViewCellEventArgs(runColumnIndex, 0));
                WaitUntil(() => relay.GetChannelState("6QMBS", 1), "Run button should execute sequence and turn CH1 on");
            }
        }

        private static void MainForm_AllOffRefreshesDevicesOnceAfterChannelUpdates()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.FourChannel, 4, 0x0f));

            using (var form = new MainForm(relay, new SequenceRepository(path)))
            {
                InvokePrivate(form, "AllOff");
            }

            for (int channel = 1; channel <= 4; channel++)
                AssertFalse(relay.GetChannelState("6QMBS", channel), "Channel " + channel + " should be off");

            AssertEqual(2, relay.EnumerateDevicesCallCount, "AllOff should enumerate for discovery and one final refresh only");
        }

        private static void MainLayoutSettings_RoundTripsWindowAndPaneSizes()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "layout.json");
            var original = new MainLayoutSettings
            {
                Left = 10,
                Top = 20,
                Width = 900,
                Height = 700,
                WindowState = FormWindowState.Normal,
                MainSplitterDistance = 360,
                SequenceListPercent = 45,
                DeviceListPercent = 65
            };

            original.Save(path);
            var loaded = MainLayoutSettings.Load(path);

            AssertEqual(original.Left, loaded.Left, "Left");
            AssertEqual(original.Top, loaded.Top, "Top");
            AssertEqual(original.Width, loaded.Width, "Width");
            AssertEqual(original.Height, loaded.Height, "Height");
            AssertEqual(original.MainSplitterDistance, loaded.MainSplitterDistance, "MainSplitterDistance");
            AssertEqual(original.SequenceListPercent, loaded.SequenceListPercent, "SequenceListPercent");
            AssertEqual(original.DeviceListPercent, loaded.DeviceListPercent, "DeviceListPercent");
        }

        private static void MainForm_SavesLayoutSettings()
        {
            string sequencePath = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            string layoutPath = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "layout.json");

            using (var form = new MainForm(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                new SequenceRepository(sequencePath),
                layoutPath))
            {
                form.SetBounds(20, 30, 900, 650);
                InvokePrivate(form, "SaveLayoutSettings");
            }

            var loaded = MainLayoutSettings.Load(layoutPath);
            AssertEqual(20, loaded.Left, "Saved Left");
            AssertEqual(30, loaded.Top, "Saved Top");
            AssertEqual(900, loaded.Width, "Saved Width");
            AssertEqual(650, loaded.Height, "Saved Height");
        }

        private static void SequenceEditorLayoutSettings_RoundTripsWindowAndSplitter()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequence-editor-layout.json");
            var original = new SequenceEditorLayoutSettings
            {
                Left = 11,
                Top = 22,
                Width = 930,
                Height = 610,
                WindowState = FormWindowState.Normal,
                SplitterDistance = 310
            };

            original.Save(path);
            var loaded = SequenceEditorLayoutSettings.Load(path);

            AssertEqual(original.Left, loaded.Left, "Editor Left");
            AssertEqual(original.Top, loaded.Top, "Editor Top");
            AssertEqual(original.Width, loaded.Width, "Editor Width");
            AssertEqual(original.Height, loaded.Height, "Editor Height");
            AssertEqual(original.SplitterDistance, loaded.SplitterDistance, "Editor SplitterDistance");
        }

        private static void SequenceEditorForm_SavesLayoutSettings()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequence-editor-layout.json");

            using (var form = new SequenceEditorForm(null, path))
            {
                form.SetBounds(12, 34, 920, 600);
                var split = (SplitContainer)GetPrivateField(form, "splitContainer");
                split.SplitterDistance = 300;
                InvokePrivate(form, "SaveLayoutSettings");
            }

            var loaded = SequenceEditorLayoutSettings.Load(path);
            AssertEqual(12, loaded.Left, "Editor form saved Left");
            AssertEqual(34, loaded.Top, "Editor form saved Top");
            AssertEqual(920, loaded.Width, "Editor form saved Width");
            AssertEqual(600, loaded.Height, "Editor form saved Height");
            AssertEqual(300, loaded.SplitterDistance, "Editor form saved SplitterDistance");
        }

        private static void InvokePrivate(object instance, string methodName, params object[] arguments)
        {
            instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments);
        }

        private static object GetPrivateField(object instance, string fieldName)
        {
            return instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static object SelectStartupMode(string[] args, bool hasInheritedConsole)
        {
            var programType = typeof(MainForm).Assembly.GetType("usbrelay.Program", true);
            var method = programType.GetMethod("SelectStartupMode", BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(method != null, "Program.SelectStartupMode should exist");
            return method.Invoke(null, new object[] { args, hasInheritedConsole });
        }

        private static object ParseCliCommand(string[] args)
        {
            var programType = typeof(MainForm).Assembly.GetType("usbrelay.UsbRelayCli", true);
            var method = programType.GetMethod("ParseCommand", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            AssertTrue(method != null, "UsbRelayCli.ParseCommand should exist");
            return method.Invoke(null, new object[] { args });
        }

        private static bool HasInheritedConsoleProcessCount(uint processCount)
        {
            var consoleWindowType = typeof(MainForm).Assembly.GetType("usbrelay.ConsoleWindow", true);
            var method = consoleWindowType.GetMethod("HasInheritedConsoleProcessCount", BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(method != null, "ConsoleWindow.HasInheritedConsoleProcessCount should exist");
            return (bool)method.Invoke(null, new object[] { processCount });
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            return (T)instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(instance);
        }

        private static string FindRepoFile(string fileName)
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                    return candidate;

                DirectoryInfo parent = Directory.GetParent(directory);
                directory = parent == null ? null : parent.FullName;
            }

            throw new FileNotFoundException("Could not find repository file.", fileName);
        }

        private static string ReadXmlProperty(string path, string propertyName)
        {
            string startTag = "<" + propertyName + ">";
            string endTag = "</" + propertyName + ">";
            string content = File.ReadAllText(path);
            int start = content.IndexOf(startTag, StringComparison.Ordinal);
            int end = content.IndexOf(endTag, StringComparison.Ordinal);

            if (start < 0 || end < 0 || end <= start)
                throw new InvalidOperationException("Property " + propertyName + " was not found in " + path);

            return content.Substring(start + startTag.Length, end - start - startTag.Length).Trim();
        }

        private static string[] RunCompletion(string commandLine)
        {
            ProcessResult result = RunUsbRelay("complete", "--position", commandLine.Length.ToString(), "--line", commandLine);

            AssertEqual(0, result.ExitCode, "Completion exit code");
            AssertEqual(string.Empty, result.Error, "Completion stderr");
            return result.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static ProcessResult RunUsbRelay(params string[] args)
        {
            string executablePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "usbrelay.exe");
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = string.Join(" ", args.Select(EscapeProcessArgument))
            };

            using (var process = Process.Start(startInfo))
            {
                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); } catch { }
                    throw new InvalidOperationException("usbrelay process did not exit: " + startInfo.Arguments);
                }

                return new ProcessResult(
                    process.ExitCode,
                    process.StandardOutput.ReadToEnd(),
                    process.StandardError.ReadToEnd());
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but was " + actual);
        }

        private static void AssertTrue(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name);
        }

        private static void AssertFalse(bool value, string name)
        {
            if (value)
                throw new InvalidOperationException(name);
        }

        private static void AssertContains<T>(IEnumerable<T> values, T expected, string name)
        {
            if (!values.Contains(expected))
                throw new InvalidOperationException(name);
        }

        private static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string name)
        {
            string expectedText = string.Join(", ", expected);
            string actualText = string.Join(", ", actual);
            if (expectedText != actualText)
                throw new InvalidOperationException(name + " expected [" + expectedText + "] but was [" + actualText + "]");
        }

        private static string EscapeProcessArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            bool needsQuotes = argument.Any(char.IsWhiteSpace) || argument.Contains("\"");
            if (!needsQuotes)
                return argument;

            var escaped = new StringBuilder();
            escaped.Append('"');
            int backslashes = 0;
            foreach (char ch in argument)
            {
                if (ch == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (ch == '"')
                {
                    escaped.Append('\\', backslashes * 2 + 1);
                    escaped.Append(ch);
                    backslashes = 0;
                    continue;
                }

                escaped.Append('\\', backslashes);
                backslashes = 0;
                escaped.Append(ch);
            }

            escaped.Append('\\', backslashes * 2);
            escaped.Append('"');
            return escaped.ToString();
        }

        private static void WaitUntil(Func<bool> condition, string failure)
        {
            DateTime deadline = DateTime.Now.AddSeconds(2);
            while (DateTime.Now < deadline)
            {
                Application.DoEvents();
                if (condition())
                    return;
                System.Threading.Thread.Sleep(10);
            }

            throw new InvalidOperationException(failure);
        }

        private static void KillStuckTestChildren(DateTime startedAfter)
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)))
            {
                try
                {
                    if (process.Id != currentProcessId && process.StartTime >= startedAfter)
                        process.Kill();
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }
        }
    }
}
