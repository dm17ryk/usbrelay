using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
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
                SequenceParser_ParsesNamedDeviceAndChannel,
                SequenceCompletionProvider_SuggestsSequenceCommands,
                SequenceCompletionProvider_SuggestsRelayStateValues,
                SequenceCompletionProvider_UsesConnectedDeviceChannels,
                SequenceCompletionProvider_SuggestsNamedDeviceAndChannel,
                SequenceCompletionProvider_FallsBackWhenNoDevicesAreConnected,
                SequenceCompletionProvider_SuggestsOutputMatchesForToolVariables,
                SequenceParseCache_ReusesParseUntilScriptChanges,
                SequenceResourceLocks_BlockOverlappingChannelsOnly,
                SequenceResourceLocks_DistinguishesDuplicateSerialDevicePaths,
                SequenceRunner_ExecutesRegexSuccessBranchWithFakeRelayAndTool,
                SequenceRunner_ExecutesConfirmationBranches,
                SequenceRunner_ExitsSuccessfullyAndSkipsRemainingActions,
                SequenceRunner_FailStillReportsFailure,
                SequenceRunner_LogsFriendlyDeviceAndChannelNames,
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
                Program_ParseCliCommand_ParsesDevicePathSelector,
                Program_ParseCliCommand_ParsesFriendlySelectors,
                Program_ParseCliCommand_ParsesNameUpdateOptions,
                Program_ParseCliCommand_RejectsGuiWithRelayOptions,
                Program_CompletionSuggestsTopLevelOptions,
                Program_CompletionSuggestsMatchingOptions,
                Program_CompletionSuggestsOnChannels,
                Program_CompletionSuggestsOffChannels,
                Program_CompletionSuggestsSequenceCommand,
                Program_CompletionSuggestsSequenceSubcommands,
                Program_CompletionSuggestsLegacyAliases,
                Program_CompletionHandlesQuotedExecutablePath,
                Program_HelpDoesNotShowCompletionCommand,
                Program_HelpArgumentPrintsSequenceExamples,
                Program_NoArgumentTerminalRunPrintsUsageAndExits,
                Program_HelpArgumentPrintsHelpAndExits,
                Program_HelpArgumentPrintsExamples,
                Program_VersionArgumentPrintsVersionAndExits,
                Program_AssemblyVersionMatchesVersionProps,
                Program_ProjectBuildsAsWindowsGuiExecutable,
                Program_SequenceQueryUsesInteractiveOutputSeparator,
                Program_SequenceRunRequiresNameThroughGrammar,
                Program_SequenceCommandRejectsUnexpectedArgumentThroughGrammar,
                Program_ReopenedConsoleUtf8EncodingDoesNotEmitPreamble,
                Program_ReopenedConsoleUtf8EncodingPreservesFallbacks,
                SequenceCli_QueryListsSavedSequences,
                SequenceCli_QueryByNamePrintsDetails,
                SequenceCli_AddReadModifyAndPrintsFunctions,
                SequenceCli_QueryTableHasSeparatorLine,
                SequenceCli_StatusReportsReadySequence,
                SequenceCli_StatusFailsWhenRelayResourceIsMissing,
                SequenceCli_StatusReportsRelayEnumerationFailureDetails,
                SequenceCli_QueryWritesSummaryInSingleOutputCall,
                SequenceCli_QueryCleanLinePrefixIsPartOfSingleOutputCall,
                SequenceCli_QueryByNameWritesDetailInSingleOutputCall,
                SequenceCli_StatusWritesReadinessInSingleOutputCall,
                SequenceCli_StatusCleanLinePrefixIsPartOfSingleOutputCall,
                SequenceCli_RunExecutesSavedSequence,
                SequenceCli_RunAutoAcceptsConfirmation,
                SequenceCli_RunCleanLinePrefixPrecedesStartedLine,
                SequenceCli_RunFailsForInvalidMissingOrDuplicateName,
                SequenceCli_RunFailsGracefullyWhenRepositoryCannotLoad,
                MainForm_LoadsSavedSequencesIntoVisibleRows,
                MainForm_RunButtonClickExecutesVisibleSequence,
                MainForm_ConfirmationReceivesTitleAndMessage,
                MainForm_DisablesBusySequenceRunButton,
                MainForm_RemoveSequenceCancelKeepsSequence,
                MainForm_RemoveSequenceConfirmDeletesSequence,
                MainForm_AllOffRefreshesDevicesOnceAfterChannelUpdates,
                MainLayoutSettings_RoundTripsWindowAndPaneSizes,
                MainForm_SavesLayoutSettings,
                SequenceEditorLayoutSettings_RoundTripsWindowAndSplitter,
                SequenceEditorForm_StoresConnectedDevicesForCompletion,
                SequenceEditorForm_SavesLayoutSettings,
                RelayNamingRepository_RoundTripsFriendlyNames,
                RelayService_RoutesByFriendlyDeviceAndChannelNames,
                RelayService_UpdatesFriendlyNames,
                DeviceNamingForm_PopulatesExistingFriendlyNames,
                DeviceNamingForm_PreservesSeparateDeviceValuesWhenSwitching,
                UsbRelayWrapper_StatusAlignsDevicePathColumn
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

        private static void SequenceParser_ParsesNamedDeviceAndChannel()
        {
            var result = SequenceParser.Parse(string.Join(Environment.NewLine, new[]
            {
                "sequence.PowerOn(\"DUT Power\", \"Main relay\");",
                "sequence.ReadChannel(\"DUT Power\", \"Main relay\");",
                "sequence.WaitChannel(\"DUT Power\", \"Main relay\", RelayState.On, 3000);"
            }));

            AssertTrue(result.IsValid, "Named device/channel script should validate");
            AssertTrue(result.Resources.Contains(new RelayResource("DUT Power", "Main relay")), "Named channel resource should be claimed");
        }

        private static void SequenceCompletionProvider_SuggestsSequenceCommands()
        {
            var result = SequenceCompletionProvider.GetCompletions("sequence.", "sequence.".Length, new RelayDevice[0], force: false);
            string[] completions = result.Items.Select(item => item.Text).ToArray();

            AssertContains(completions, "PowerOn(\"6QMBS\", 1)", "PowerOn completion");
            AssertContains(completions, "PowerOff(\"6QMBS\", 1)", "PowerOff completion");
            AssertContains(completions, "Sleep(500)", "Sleep completion");
            AssertContains(completions, "ReadChannel(\"6QMBS\", 1)", "ReadChannel completion");
            AssertContains(completions, "WaitChannel(\"6QMBS\", 1, RelayState.On, 3000)", "WaitChannel completion");
            AssertContains(completions, "RunTool(\"tool.exe\", \"--args\")", "RunTool completion");
            AssertContains(completions, "Confirm(\"title\", \"message\")", "Confirm completion");
            AssertContains(completions, "Fail(\"message\")", "Fail completion");
            AssertContains(completions, "Exit(\"message\")", "Exit completion");
        }

        private static void SequenceCompletionProvider_SuggestsRelayStateValues()
        {
            var result = SequenceCompletionProvider.GetCompletions("sequence.WaitChannel(\"6QMBS\", 1, RelayState.", "sequence.WaitChannel(\"6QMBS\", 1, RelayState.".Length, new RelayDevice[0], force: false);

            AssertSequence(new[] { "Off", "On" }, result.Items.Select(item => item.Text), "RelayState completions");
        }

        private static void SequenceCompletionProvider_UsesConnectedDeviceChannels()
        {
            var devices = new[] { new RelayDevice("ABC123", RelayDeviceType.FourChannel, 4, 0) };
            var result = SequenceCompletionProvider.GetCompletions("sequence.", "sequence.".Length, devices, force: false);
            string[] completions = result.Items.Select(item => item.Text).ToArray();

            AssertContains(completions, "PowerOn(\"ABC123\", 4)", "Connected device channel completion");
            AssertFalse(completions.Contains("PowerOn(\"6QMBS\", 1)"), "Fallback serial should not be used when connected devices exist");
        }

        private static void SequenceCompletionProvider_SuggestsNamedDeviceAndChannel()
        {
            var devices = new[]
            {
                new RelayDevice(
                    "6QMBS",
                    RelayDeviceType.EightChannel,
                    8,
                    0,
                    "hid#board-a",
                    "DUT Power",
                    new Dictionary<int, string> { { 1, "Main relay" } })
            };
            var result = SequenceCompletionProvider.GetCompletions("sequence.", "sequence.".Length, devices, force: false);

            AssertContains(result.Items.Select(item => item.Text), "PowerOn(\"DUT Power\", \"Main relay\")", "Named channel completion");
        }

        private static void SequenceCompletionProvider_FallsBackWhenNoDevicesAreConnected()
        {
            var result = SequenceCompletionProvider.GetCompletions("sequence.", "sequence.".Length, new RelayDevice[0], force: false);

            AssertContains(result.Items.Select(item => item.Text), "PowerOn(\"6QMBS\", 1)", "Fallback completion");
        }

        private static void SequenceCompletionProvider_SuggestsOutputMatchesForToolVariables()
        {
            string script = "var probe = sequence.RunTool(\"tool.exe\", \"--probe\");" + Environment.NewLine + "if (probe.";
            var result = SequenceCompletionProvider.GetCompletions(script, script.Length, new RelayDevice[0], force: false);

            AssertContains(result.Items.Select(item => item.Text), "OutputMatches(\"READY|OK\")", "OutputMatches completion");
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

        private static void SequenceResourceLocks_DistinguishesDuplicateSerialDevicePaths()
        {
            var locks = new SequenceResourceLocks();
            var firstBoard = new[] { new RelayResource("BITFT", 1, "hid#board-a") };
            var secondBoard = new[] { new RelayResource("BITFT", 1, "hid#board-b") };

            AssertTrue(locks.TryReserve("first-board", firstBoard), "First duplicate-serial board should reserve CH1");
            AssertTrue(locks.TryReserve("second-board", secondBoard), "Second duplicate-serial board should reserve CH1 independently");
            AssertTrue(
                new RelayResource("BITFT", 1, "hid#board-a")
                    .Equals(new RelayResource("BITFT", 1, "hid#board-a")),
                "The same board path should remain equal");
            AssertFalse(
                new RelayResource("BITFT", 1, "hid#board-a")
                    .Equals(new RelayResource("BITFT", 1, "hid#board-b")),
                "Different board paths must not compare equal");
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

        private static void SequenceRunner_ExecutesConfirmationBranches()
        {
            string script = string.Join(Environment.NewLine, new[]
            {
                "var result = sequence.Confirm(\"Confirm power\", \"Start power?\");",
                "if (result) {",
                "    sequence.PowerOn(\"6QMBS\", 1);",
                "} else {",
                "    sequence.PowerOn(\"6QMBS\", 2);",
                "}"
            });
            SequenceParseResult parsed = SequenceParser.Parse(script);
            AssertTrue(parsed.IsValid, "Confirmation branch script should parse");

            string trueTitle = null;
            string trueMessage = null;
            var trueRelay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            SequenceRunResult trueResult = SequenceRunner.Run(
                parsed,
                trueRelay,
                new FakeExternalToolRunner(string.Empty),
                confirmation: (title, message) =>
                {
                    trueTitle = title;
                    trueMessage = message;
                    return true;
                });

            AssertTrue(trueResult.Success, "Confirmed sequence should succeed");
            AssertFalse(trueResult.Exited, "Confirmed sequence should not be marked exited");
            AssertTrue(trueRelay.GetChannelState("6QMBS", 1), "OK branch should turn CH1 on");
            AssertFalse(trueRelay.GetChannelState("6QMBS", 2), "OK branch should not turn CH2 on");
            AssertEqual("Confirm power", trueTitle, "Confirmation title");
            AssertEqual("Start power?", trueMessage, "Confirmation message");

            var falseRelay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            SequenceRunResult falseResult = SequenceRunner.Run(
                parsed,
                falseRelay,
                new FakeExternalToolRunner(string.Empty),
                confirmation: (title, message) => false);

            AssertTrue(falseResult.Success, "Cancelled branch should still complete successfully");
            AssertFalse(falseResult.Exited, "Cancelled branch without Exit should not be marked exited");
            AssertFalse(falseRelay.GetChannelState("6QMBS", 1), "Cancel branch should not turn CH1 on");
            AssertTrue(falseRelay.GetChannelState("6QMBS", 2), "Cancel branch should turn CH2 on");
            AssertTrue(falseResult.Log.Any(line => line.Contains("stored boolean variable result = False")), "Confirmation result should be logged");
        }

        private static void SequenceRunner_ExitsSuccessfullyAndSkipsRemainingActions()
        {
            string script = string.Join(Environment.NewLine, new[]
            {
                "var result = sequence.Confirm(\"Confirm power\", \"Start power?\");",
                "if (!result) {",
                "    sequence.Exit(\"User cancelled\");",
                "}",
                "sequence.PowerOn(\"6QMBS\", 1);"
            });
            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));

            SequenceRunResult result = SequenceRunner.Run(
                SequenceParser.Parse(script),
                relay,
                new FakeExternalToolRunner(string.Empty),
                confirmation: (title, message) => false);

            AssertTrue(result.Success, "Exit should be a successful run");
            AssertTrue(result.Exited, "Exit should mark the run as exited");
            AssertTrue(result.Error == null, "Exit should not expose an error");
            AssertFalse(relay.GetChannelState("6QMBS", 1), "Actions after Exit should not execute");
            AssertTrue(result.Log.Any(line => line.Contains("EXIT: User cancelled")), "Exit message should be logged");
        }

        private static void SequenceRunner_FailStillReportsFailure()
        {
            SequenceRunResult result = SequenceRunner.Run(
                SequenceParser.Parse("sequence.Fail(\"Expected failure\");"),
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                new FakeExternalToolRunner(string.Empty));

            AssertFalse(result.Success, "Fail should report an unsuccessful run");
            AssertFalse(result.Exited, "Fail should not report a graceful exit");
            AssertTrue(result.Error != null, "Fail should expose an error");
        }

        private static void SequenceRunner_LogsFriendlyDeviceAndChannelNames()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var device = new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0, "hid#board-a");
            var repository = new RelayNamingRepository(path);
            var configuration = new RelayNamingConfiguration();
            configuration.Devices.Add(new RelayDeviceNaming
            {
                DeviceKey = RelayNamingRepository.GetDeviceKey(device),
                Name = "DUT Power",
                Channels = new List<RelayChannelNaming> { new RelayChannelNaming { Channel = 1, Name = "Main relay" } }
            });
            repository.Save(configuration);

            var result = SequenceRunner.Run(
                SequenceParser.Parse("sequence.PowerOn(\"6QMBS\", 1);"),
                new RelaySequenceBackend(new RelayService(new FakeRelayBackend(device), repository)),
                new FakeExternalToolRunner(string.Empty),
                skipDelays: true);

            AssertTrue(result.Success, "Friendly-name sequence should succeed");
            AssertTrue(result.Log.Any(line => line.Contains("DUT Power Main relay (CH1) -> ON ok")), "Sequence log should use friendly device and channel names");
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

        private static void Program_ParseCliCommand_ParsesDevicePathSelector()
        {
            object command = ParseCliCommand(new[] { "--device-path", "hid#board-b", "--on", "1" });

            AssertTrue(GetProperty<bool>(command, "IsValid"), "Device path selector should be valid");
            AssertEqual("hid#board-b", GetProperty<string>(command, "DevicePath"), "Device path option");
            AssertEqual("ONOFF", GetProperty<object>(command, "Operation").ToString(), "Relay operation");
        }

        private static void Program_ParseCliCommand_ParsesFriendlySelectors()
        {
            object command = ParseCliCommand(new[]
            {
                "--device-name", "DUT Power",
                "--on-name", "Main relay", "Aux relay",
                "--off-name", "Fan"
            });

            AssertTrue(GetProperty<bool>(command, "IsValid"), "Friendly selectors should be valid");
            AssertEqual("DUT Power", GetProperty<string>(command, "DeviceName"), "Device name option");
            AssertSequence(new[] { "Main relay", "Aux relay" }, GetProperty<IEnumerable<string>>(command, "OnChannelNames"), "Named on channels");
            AssertSequence(new[] { "Fan" }, GetProperty<IEnumerable<string>>(command, "OffChannelNames"), "Named off channels");
        }

        private static void Program_ParseCliCommand_ParsesNameUpdateOptions()
        {
            object command = ParseCliCommand(new[]
            {
                "--device-path", "hid#board-a",
                "--set-device-name", "DUT Power",
                "--set-channel-name", "1", "Main relay", "2", "Debug"
            });

            AssertTrue(GetProperty<bool>(command, "IsValid"), "Name update options should be valid");
            AssertEqual("NAMES", GetProperty<object>(command, "Operation").ToString(), "Name update operation");
            AssertEqual("DUT Power", GetProperty<string>(command, "SetDeviceName"), "Set device name option");
            AssertSequence(new[] { "1", "Main relay", "2", "Debug" }, GetProperty<IEnumerable<string>>(command, "SetChannelNamePairs"), "Set channel name pairs");
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
            AssertContains(completions, "--device-path", "Top-level completions should include --device-path");
            AssertContains(completions, "--device-name", "Top-level completions should include --device-name");
            AssertContains(completions, "--set-device-name", "Top-level completions should include --set-device-name");
            AssertContains(completions, "--set-channel-name", "Top-level completions should include --set-channel-name");
            AssertContains(completions, "--on-name", "Top-level completions should include --on-name");
            AssertContains(completions, "--off-name", "Top-level completions should include --off-name");
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

        private static void Program_CompletionSuggestsSequenceCommand()
        {
            string[] completions = RunCompletion("usbrelay ");

            AssertContains(completions, "sequence", "Top-level completions should include sequence command");
        }

        private static void Program_CompletionSuggestsSequenceSubcommands()
        {
            string[] completions = RunCompletion("usbrelay sequence ");

            AssertContains(completions, "query", "Sequence completions should include query");
            AssertContains(completions, "read", "Sequence completions should include read");
            AssertContains(completions, "add", "Sequence completions should include add");
            AssertContains(completions, "modify", "Sequence completions should include modify");
            AssertContains(completions, "status", "Sequence completions should include status");
            AssertContains(completions, "run", "Sequence completions should include run");
            AssertContains(completions, "functions", "Sequence completions should include functions");
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

        private static void Program_HelpArgumentPrintsSequenceExamples()
        {
            ProcessResult result = RunUsbRelay("--help");

            AssertEqual(0, result.ExitCode, "--help exit code");
            AssertTrue(result.Output.Contains("usbrelay sequence query"), "Help should include sequence query example");
            AssertTrue(result.Output.Contains("usbrelay sequence add"), "Help should include sequence add example");
            AssertTrue(result.Output.Contains("usbrelay sequence modify"), "Help should include sequence modify example");
            AssertTrue(result.Output.Contains("usbrelay sequence functions"), "Help should include sequence functions example");
            AssertTrue(result.Output.Contains("usbrelay sequence status --name"), "Help should include sequence status example");
            AssertTrue(result.Output.Contains("usbrelay sequence run --name"), "Help should include sequence run example");
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

        private static void Program_ProjectBuildsAsWindowsGuiExecutable()
        {
            string solutionPath = FindRepoFile("usbrelay.sln");
            string projectPath = Path.Combine(Path.GetDirectoryName(solutionPath), "usbrelay", "usbrelay.csproj");
            string outputType = ReadXmlProperty(projectPath, "OutputType");

            AssertEqual("WinExe", outputType, "GUI executable should not create a console window on shell launch");
        }

        private static void Program_SequenceQueryUsesInteractiveOutputSeparator()
        {
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("query", false), "Interactive sequence query should start on a clean line");
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("status", false), "Interactive sequence status should start on a clean line");
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("run", false), "Interactive sequence run should start on a clean line");
            AssertFalse(ShouldWriteInteractiveSequenceSeparator("query", true), "Redirected sequence query should not get an extra leading line");
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("read", false), "Interactive sequence read should start on a clean line");
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("add", false), "Interactive sequence add should start on a clean line");
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("modify", false), "Interactive sequence modify should start on a clean line");
            AssertTrue(ShouldWriteInteractiveSequenceSeparator("functions", false), "Interactive sequence functions should start on a clean line");
            AssertFalse(ShouldWriteInteractiveSequenceSeparator("status", true), "Redirected sequence status should not get an extra leading line");
            AssertFalse(ShouldWriteInteractiveSequenceSeparator("run", true), "Redirected sequence run should not get an extra leading line");
            AssertFalse(ShouldWriteInteractiveSequenceSeparator("unknown", false), "Unknown sequence command should not get an extra leading line");
        }

        private static void Program_SequenceRunRequiresNameThroughGrammar()
        {
            ProcessResult result = RunUsbRelay("sequence", "run");

            AssertEqual(1, result.ExitCode, "sequence run without --name exit code");
            AssertTrue(result.Error.Contains("--name"), "sequence run without --name should use grammar required-option error");
        }

        private static void Program_SequenceCommandRejectsUnexpectedArgumentThroughGrammar()
        {
            ProcessResult result = RunUsbRelay("sequence", "query", "unexpected");

            AssertEqual(1, result.ExitCode, "sequence query unexpected argument exit code");
            AssertTrue(result.Error.Contains("unexpected"), "sequence query unexpected argument should be reported by grammar");
        }

        private static void Program_ReopenedConsoleUtf8EncodingDoesNotEmitPreamble()
        {
            Encoding encoding = CreateConsoleStreamEncoding(Encoding.UTF8);

            AssertEqual(Encoding.UTF8.CodePage, encoding.CodePage, "Console stream encoding should keep UTF-8 code page");
            AssertEqual(0, encoding.GetPreamble().Length, "Console stream UTF-8 encoding should not emit a BOM");
        }

        private static void Program_ReopenedConsoleUtf8EncodingPreservesFallbacks()
        {
            Encoding strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
            Encoding encoding = CreateConsoleStreamEncoding(strictUtf8);

            AssertEqual(Encoding.UTF8.CodePage, encoding.CodePage, "Strict console stream encoding should keep UTF-8 code page");
            AssertEqual(0, encoding.GetPreamble().Length, "Strict console stream UTF-8 encoding should not emit a BOM");
            AssertEqual(strictUtf8.EncoderFallback.GetType(), encoding.EncoderFallback.GetType(), "Console stream UTF-8 encoding should preserve encoder fallback");
            AssertEqual(strictUtf8.DecoderFallback.GetType(), encoding.DecoderFallback.GetType(), "Console stream UTF-8 encoding should preserve decoder fallback");

            bool decoderThrew = false;
            try
            {
                encoding.GetString(new byte[] { 0xff });
            }
            catch (DecoderFallbackException)
            {
                decoderThrew = true;
            }

            AssertTrue(decoderThrew, "Console stream UTF-8 encoding should keep strict invalid-byte behavior");
        }

        private static void SequenceCli_QueryListsSavedSequences()
        {
            var harness = CreateSequenceCliHarness(
                new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition
                {
                    Name = "Power cycle DUT",
                    RunButtonText = "Cycle",
                    Description = "Cycle DUT power",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Query(null);

            AssertEqual(0, exitCode, "Query exit code");
            AssertTrue(harness.Output.ToString().Contains("Power cycle DUT"), "Query should list sequence name");
            AssertTrue(harness.Output.ToString().Contains("Cycle"), "Query should list run button text");
            AssertTrue(harness.Output.ToString().Contains("Valid"), "Query should list validity");
            AssertTrue(harness.Output.ToString().Contains("6QMBS:CH1"), "Query should list resources");
            AssertEqual(string.Empty, harness.Error.ToString(), "Query stderr");
        }

        private static void SequenceCli_QueryByNamePrintsDetails()
        {
            var harness = CreateSequenceCliHarness(
                new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition
                {
                    Name = "Inspect",
                    RunButtonText = "Inspect",
                    Description = "Detailed sequence",
                    Script = "sequence.ReadChannel(\"6QMBS\", 2);"
                });

            int exitCode = harness.Cli.Query("inspect");

            AssertEqual(0, exitCode, "Query detail exit code");
            AssertTrue(harness.Output.ToString().Contains("Name: Inspect"), "Query detail should include name");
            AssertTrue(harness.Output.ToString().Contains("Description: Detailed sequence"), "Query detail should include description");
            AssertTrue(harness.Output.ToString().Contains("sequence.ReadChannel(\"6QMBS\", 2);"), "Query detail should include script");
            AssertTrue(harness.Output.ToString().Contains("6QMBS:CH2"), "Query detail should include resources");
        }

        private static void SequenceCli_QueryTableHasSeparatorLine()
        {
            var harness = CreateSequenceCliHarness(
                new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition
                {
                    Name = "CP Reset",
                    RunButtonText = "CP Reset",
                    Description = "Reset CP",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Query(null);
            string output = harness.Output.ToString();
            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            AssertEqual(0, exitCode, "Query table separator exit code");
            AssertTrue(lines.Length >= 3, "Query table should contain header, separator, and row");
            AssertTrue(lines[0].Contains("Name") && lines[0].Contains("Run button") && lines[0].Contains("Validity") && lines[0].Contains("Resources"), "Query table header should contain all columns");
            AssertTrue(lines[1].StartsWith("----"), "Query table should contain a separator after the header");
            AssertTrue(lines[1].All(ch => ch == '-' || ch == ' '), "Query table separator should use dashes and spaces");
            AssertTrue(lines[2].Contains("CP Reset"), "Query table row should follow separator");
            AssertFalse(output.Contains("\t"), "Query table should use fixed spacing instead of tabs");
        }

        private static void SequenceCli_StatusReportsReadySequence()
        {
            var harness = CreateSequenceCliHarness(
                new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition
                {
                    Name = "Ready",
                    RunButtonText = "Run",
                    Description = "Ready sequence",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Status("Ready");

            AssertEqual(0, exitCode, "Status ready exit code");
            AssertTrue(harness.Output.ToString().Contains("Ready: Ready"), "Status should report ready");
            AssertEqual(string.Empty, harness.Error.ToString(), "Ready status stderr");
        }

        private static void SequenceCli_StatusFailsWhenRelayResourceIsMissing()
        {
            var harness = CreateSequenceCliHarness(
                new RelayDevice("OTHER", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition
                {
                    Name = "Missing relay",
                    RunButtonText = "Run",
                    Description = "Missing sequence",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Status("Missing relay");

            AssertEqual(1, exitCode, "Status missing exit code");
            AssertTrue(harness.Output.ToString().Contains("Missing relay: Missing resources"), "Status should report missing resources");
            AssertTrue(harness.Output.ToString().Contains("6QMBS:CH1"), "Status should show missing resource");
        }

        private static void SequenceCli_StatusReportsRelayEnumerationFailureDetails()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Ready",
                    RunButtonText = "Run",
                    Description = "Ready sequence",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                }
            });
            var output = new StringWriter();
            var error = new StringWriter();
            var cli = new SequenceCli(
                repository,
                new ThrowingRelayBackend(new InvalidOperationException("native enumerate failed")),
                new FakeExternalToolRunner(string.Empty),
                output,
                error);

            int exitCode = cli.Status("Ready");
            string errorText = error.ToString();

            AssertEqual(1, exitCode, "Status relay enumeration failure exit code");
            AssertTrue(errorText.Contains("Failed to enumerate relay devices."), "Status relay enumeration failure should include operation context");
            AssertTrue(errorText.Contains("native enumerate failed"), "Status relay enumeration failure should include exception message");
            AssertTrue(errorText.Contains("System.InvalidOperationException"), "Status relay enumeration failure should include exception type");
            AssertEqual(string.Empty, output.ToString(), "Status relay enumeration failure should not print readiness output");
        }

        private static void SequenceCli_QueryWritesSummaryInSingleOutputCall()
        {
            var output = new RecordingTextWriter();
            var harness = CreateSequenceCliHarness(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                output,
                new SequenceDefinition
                {
                    Name = "CP Reset",
                    RunButtonText = "CP Reset",
                    Description = "Reset CP",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                },
                new SequenceDefinition
                {
                    Name = "CP On",
                    RunButtonText = "CP On",
                    Description = "Turn CP on",
                    Script = "sequence.PowerOn(\"6QMBS\", 2);"
                });

            int exitCode = harness.Cli.Query(null);

            AssertEqual(0, exitCode, "Query buffered exit code");
            AssertEqual(1, output.WriteCallCount, "Query should write summary output once");
            AssertTrue(output.ToString().Contains("Name"), "Query should keep summary header");
            AssertTrue(output.ToString().Contains("----"), "Query should keep summary separator");
            AssertTrue(output.ToString().Contains("CP Reset"), "Query should keep sequence rows");
        }

        private static void SequenceCli_QueryCleanLinePrefixIsPartOfSingleOutputCall()
        {
            var output = new RecordingTextWriter();
            var harness = CreateSequenceCliHarness(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                output,
                new SequenceDefinition
                {
                    Name = "CP Reset",
                    RunButtonText = "CP Reset",
                    Description = "Reset CP",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Query(null, true);

            AssertEqual(0, exitCode, "Query clean-line exit code");
            AssertEqual(1, output.WriteCallCount, "Query clean-line output should still be one write");
            AssertTrue(output.ToString().StartsWith(Environment.NewLine + "Name"), "Query clean-line output should prefix the final table write");
        }

        private static void SequenceCli_QueryByNameWritesDetailInSingleOutputCall()
        {
            var output = new RecordingTextWriter();
            var harness = CreateSequenceCliHarness(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                output,
                new SequenceDefinition
                {
                    Name = "Inspect",
                    RunButtonText = "Inspect",
                    Description = "Detailed sequence",
                    Script = "sequence.ReadChannel(\"6QMBS\", 2);"
                });

            int exitCode = harness.Cli.Query("Inspect");

            AssertEqual(0, exitCode, "Query detail buffered exit code");
            AssertEqual(1, output.WriteCallCount, "Query detail should write output once");
            AssertTrue(output.ToString().Contains("Name: Inspect"), "Query detail should keep name");
            AssertTrue(output.ToString().Contains("Script:"), "Query detail should keep script section");
        }

        private static void SequenceCli_AddReadModifyAndPrintsFunctions()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            var output = new StringWriter();
            var error = new StringWriter();
            var cli = new SequenceCli(
                repository,
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                new FakeExternalToolRunner(string.Empty),
                output,
                error);

            AssertEqual(
                0,
                cli.Add("CLI sequence", "sequence.PowerOn(\"6QMBS\", 1);", null, "Power", "Created from CLI"),
                "Sequence add exit code");
            AssertEqual(1, repository.Load().Count, "Sequence add should persist one sequence");
            AssertEqual("Created from CLI", repository.Load().Single().Description, "Sequence add description");

            output.GetStringBuilder().Clear();
            AssertEqual(0, cli.Read("CLI sequence"), "Sequence read exit code");
            AssertTrue(output.ToString().Contains("Script:"), "Sequence read should print details");

            output.GetStringBuilder().Clear();
            AssertEqual(
                0,
                cli.Modify("CLI sequence", "CLI sequence v2", "sequence.PowerOff(\"6QMBS\", 1);", null, "Power off", "Modified from CLI"),
                "Sequence modify exit code");
            SequenceDefinition modified = repository.Load().Single();
            AssertEqual("CLI sequence v2", modified.Name, "Sequence modify name");
            AssertEqual("Power off", modified.RunButtonText, "Sequence modify run button");
            AssertEqual("Modified from CLI", modified.Description, "Sequence modify description");
            AssertEqual("sequence.PowerOff(\"6QMBS\", 1);", modified.Script, "Sequence modify script");

            output.GetStringBuilder().Clear();
            AssertEqual(0, cli.Functions(), "Sequence functions exit code");
            AssertTrue(output.ToString().Contains("sequence.PowerOn"), "Functions should list PowerOn");
            AssertTrue(output.ToString().Contains("sequence.RunTool"), "Functions should list RunTool");
            AssertTrue(output.ToString().Contains("sequence.Confirm"), "Functions should list Confirm");
            AssertTrue(output.ToString().Contains("sequence.Exit"), "Functions should list Exit");
            AssertTrue(output.ToString().Contains("OutputMatches"), "Functions should list OutputMatches");
            AssertTrue(output.ToString().Contains("RelayState.On"), "Functions should list RelayState values");
            AssertEqual(string.Empty, error.ToString(), "Sequence CRUD stderr");
        }

        private static void SequenceCli_StatusWritesReadinessInSingleOutputCall()
        {
            var output = new RecordingTextWriter();
            var harness = CreateSequenceCliHarness(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                output,
                new SequenceDefinition
                {
                    Name = "Ready one",
                    RunButtonText = "Run",
                    Description = "Ready one",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                },
                new SequenceDefinition
                {
                    Name = "Ready two",
                    RunButtonText = "Run",
                    Description = "Ready two",
                    Script = "sequence.PowerOff(\"6QMBS\", 2);"
                });

            int exitCode = harness.Cli.Status(null);

            AssertEqual(0, exitCode, "Status buffered exit code");
            AssertEqual(1, output.WriteCallCount, "Status should write readiness output once");
            AssertTrue(output.ToString().Contains("Ready one: Ready"), "Status should keep first sequence line");
            AssertTrue(output.ToString().Contains("Ready two: Ready"), "Status should keep second sequence line");
        }

        private static void SequenceCli_StatusCleanLinePrefixIsPartOfSingleOutputCall()
        {
            var output = new RecordingTextWriter();
            var harness = CreateSequenceCliHarness(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                output,
                new SequenceDefinition
                {
                    Name = "Ready one",
                    RunButtonText = "Run",
                    Description = "Ready one",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Status(null, true);

            AssertEqual(0, exitCode, "Status clean-line exit code");
            AssertEqual(1, output.WriteCallCount, "Status clean-line output should still be one write");
            AssertTrue(output.ToString().StartsWith(Environment.NewLine + "Ready one: Ready"), "Status clean-line output should prefix the readiness write");
        }

        private static void SequenceCli_RunExecutesSavedSequence()
        {
            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            var harness = CreateSequenceCliHarness(
                relay,
                new SequenceDefinition
                {
                    Name = "Turn on",
                    RunButtonText = "Run",
                    Description = "Turns channel on",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Run("Turn on");

            AssertEqual(0, exitCode, "Run exit code");
            AssertTrue(relay.GetChannelState("6QMBS", 1), "Run should turn CH1 on");
            AssertTrue(harness.Output.ToString().Contains("Turn on finished"), "Run should print finish line");
            AssertEqual(string.Empty, harness.Error.ToString(), "Run stderr");
        }

        private static void SequenceCli_RunAutoAcceptsConfirmation()
        {
            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            var harness = CreateSequenceCliHarness(
                relay,
                new SequenceDefinition
                {
                    Name = "CLI confirmation",
                    RunButtonText = "Run",
                    Description = "CLI confirmation",
                    Script = string.Join(Environment.NewLine, new[]
                    {
                        "var result = sequence.Confirm(\"Confirm power\", \"Start power?\");",
                        "if (!result) {",
                        "    sequence.Exit(\"User cancelled\");",
                        "}",
                        "sequence.PowerOn(\"6QMBS\", 1);"
                    })
                });

            int exitCode = harness.Cli.Run("CLI confirmation");

            AssertEqual(0, exitCode, "CLI confirmation exit code");
            AssertTrue(relay.GetChannelState("6QMBS", 1), "CLI confirmation should auto-accept and execute the sequence");
            AssertTrue(harness.Output.ToString().Contains("auto-accepted (CLI)"), "CLI confirmation should log automatic acceptance");
            AssertTrue(harness.Output.ToString().Contains("CLI confirmation finished"), "CLI confirmation should finish normally");
            AssertEqual(string.Empty, harness.Error.ToString(), "CLI confirmation stderr");
        }

        private static void SequenceCli_RunCleanLinePrefixPrecedesStartedLine()
        {
            var output = new RecordingTextWriter();
            var harness = CreateSequenceCliHarness(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                output,
                new SequenceDefinition
                {
                    Name = "Turn on",
                    RunButtonText = "Run",
                    Description = "Turns channel on",
                    Script = "sequence.PowerOn(\"6QMBS\", 1);"
                });

            int exitCode = harness.Cli.Run("Turn on", true);

            AssertEqual(0, exitCode, "Run clean-line exit code");
            AssertTrue(output.ToString().StartsWith(Environment.NewLine + "Turn on started"), "Run clean-line output should prefix the started line");
            AssertTrue(output.WriteCallCount > 1, "Run should keep streaming log output after the started line");
        }

        private static void SequenceCli_RunFailsForInvalidMissingOrDuplicateName()
        {
            var invalid = CreateSequenceCliHarness(
                new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition
                {
                    Name = "Invalid",
                    RunButtonText = "Run",
                    Description = "Invalid",
                    Script = "sequence.Unknown();"
                });

            AssertEqual(1, invalid.Cli.Run("Invalid"), "Invalid run exit code");
            AssertTrue(invalid.Error.ToString().Contains("Unsupported sequence command"), "Invalid run should print diagnostics");

            var missing = CreateSequenceCliHarness(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            AssertEqual(1, missing.Cli.Run("Missing"), "Missing run exit code");
            AssertTrue(missing.Error.ToString().Contains("Sequence not found"), "Missing run should print not found");

            var duplicate = CreateSequenceCliHarness(
                new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0),
                new SequenceDefinition { Name = "Dup", RunButtonText = "Run", Description = "", Script = "sequence.PowerOn(\"6QMBS\", 1);" },
                new SequenceDefinition { Name = "dup", RunButtonText = "Run", Description = "", Script = "sequence.PowerOn(\"6QMBS\", 2);" });
            AssertEqual(1, duplicate.Cli.Run("DUP"), "Duplicate run exit code");
            AssertTrue(duplicate.Error.ToString().Contains("Duplicate sequence name"), "Duplicate run should print duplicate error");
        }

        private static void SequenceCli_RunFailsGracefullyWhenRepositoryCannotLoad()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{ invalid json", Encoding.UTF8);
            var output = new StringWriter();
            var error = new StringWriter();
            var cli = new SequenceCli(
                new SequenceRepository(path),
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                new FakeExternalToolRunner(string.Empty),
                output,
                error);

            int exitCode = cli.Run("Anything");
            string errorText = error.ToString();

            AssertEqual(1, exitCode, "Run repository load failure exit code");
            AssertTrue(errorText.Contains("Failed to load sequences from repository."), "Run repository load failure should include operation context");
            AssertTrue(errorText.Contains("System."), "Run repository load failure should include exception details");
            AssertEqual(string.Empty, output.ToString(), "Run repository load failure should not print run output");
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

        private static void MainForm_ConfirmationReceivesTitleAndMessage()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Confirmed GUI sequence",
                    RunButtonText = "Run",
                    Description = "GUI confirmation test",
                    Script = string.Join(Environment.NewLine, new[]
                    {
                        "var result = sequence.Confirm(\"GUI title\", \"GUI message\");",
                        "if (result) {",
                        "    sequence.PowerOn(\"6QMBS\", 1);",
                        "}"
                    })
                }
            });

            string title = null;
            string message = null;
            var relay = new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0));
            using (var form = new MainForm(
                relay,
                repository,
                null,
                null,
                (confirmationTitle, confirmationMessage) =>
                {
                    title = confirmationTitle;
                    message = confirmationMessage;
                    return true;
                }))
            {
                InvokePrivate(form, "LoadSequences");
                var sequenceList = (DataGridView)GetPrivateField(form, "sequenceGrid");
                int runColumnIndex = sequenceList.Columns["RunColumn"].Index;

                InvokePrivate(form, "SequenceGrid_CellClick", sequenceList, new DataGridViewCellEventArgs(runColumnIndex, 0));
                WaitUntil(() => relay.GetChannelState("6QMBS", 1), "GUI confirmation should allow the sequence to run");
            }

            AssertEqual("GUI title", title, "GUI confirmation title");
            AssertEqual("GUI message", message, "GUI confirmation message");
        }

        private static void MainForm_DisablesBusySequenceRunButton()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Busy sequence",
                    RunButtonText = "Run",
                    Description = "Busy test",
                    Script = "sequence.PowerOn(\"BUSYTEST\", 1);" + Environment.NewLine + "sequence.Sleep(1200);" + Environment.NewLine + "sequence.PowerOff(\"BUSYTEST\", 1);"
                }
            });

            var relay = new FakeRelayBackend(new RelayDevice("BUSYTEST", RelayDeviceType.EightChannel, 8, 0));
            using (var form = new MainForm(relay, repository))
            {
                IntPtr formHandle = form.Handle;
                InvokePrivate(form, "RefreshDevices");
                InvokePrivate(form, "LoadSequences");
                var sequenceList = (DataGridView)GetPrivateField(form, "sequenceGrid");
                var statusGrid = (DataGridView)GetPrivateField(form, "statusGrid");
                var devicesPanel = (FlowLayoutPanel)GetPrivateField(form, "devicesPanel");
                var channelButton = (Button)((FlowLayoutPanel)devicesPanel.Controls[0].Controls[0]).Controls[0];
                int runColumnIndex = sequenceList.Columns["RunColumn"].Index;

                InvokePrivate(form, "SequenceGrid_CellClick", sequenceList, new DataGridViewCellEventArgs(runColumnIndex, 0));
                AssertEqual("Busy", sequenceList.Rows[0].Cells["RunColumn"].Value, "Sequence run button should show Busy immediately");
                AssertTrue(sequenceList.Rows[0].Cells["RunColumn"].ReadOnly, "Busy sequence run button should be disabled");
                AssertFalse(channelButton.Enabled, "Busy sequence channel button should be disabled");

                WaitUntil(() => string.Equals(sequenceList.Rows[0].Cells["RunColumn"].Value as string, "Run", StringComparison.Ordinal), "Sequence run button should be restored after completion");
                AssertFalse(sequenceList.Rows[0].Cells["RunColumn"].ReadOnly, "Completed sequence run button should be enabled");
                var refreshedChannelButton = (Button)((FlowLayoutPanel)devicesPanel.Controls[0].Controls[0]).Controls[0];
                AssertTrue(refreshedChannelButton.Enabled, "Completed sequence channel button should be enabled");

                relay.SetChannel(new RelayDevice("BUSYTEST", RelayDeviceType.EightChannel, 8, 0), 1, true);
                InvokePrivate(form, "RefreshDeviceStatusTable");
                AssertTrue(((statusGrid.Rows[0].Cells[4].Value as string) ?? string.Empty).Contains("ON"), "Status table should show a refreshed ON state");
                relay.SetChannel(new RelayDevice("BUSYTEST", RelayDeviceType.EightChannel, 8, 0), 1, false);
                InvokePrivate(form, "RefreshDeviceStatusTable");
                AssertTrue(((statusGrid.Rows[0].Cells[4].Value as string) ?? string.Empty).Contains("OFF"), "Status table should show a refreshed OFF state");
            }
        }

        private static void MainForm_RemoveSequenceCancelKeepsSequence()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Protected sequence",
                    RunButtonText = "Run",
                    Description = "Do not remove",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                }
            });

            bool confirmationRequested = false;
            using (var form = new MainForm(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                repository,
                null,
                sequence =>
                {
                    confirmationRequested = true;
                    AssertEqual("Protected sequence", sequence.Name, "Confirmation sequence name");
                    return false;
                }))
            {
                InvokePrivate(form, "LoadSequences");
                SelectFirstSequence(form);
                InvokePrivate(form, "RemoveSequence");
            }

            AssertTrue(confirmationRequested, "Remove should request confirmation");
            AssertEqual(1, repository.Load().Count, "Cancel should keep saved sequence");
        }

        private static void MainForm_RemoveSequenceConfirmDeletesSequence()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(new[]
            {
                new SequenceDefinition
                {
                    Name = "Removable sequence",
                    RunButtonText = "Run",
                    Description = "Remove",
                    Script = "sequence.PowerOff(\"6QMBS\", 1);"
                }
            });

            using (var form = new MainForm(
                new FakeRelayBackend(new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0)),
                repository,
                null,
                sequence => true))
            {
                InvokePrivate(form, "LoadSequences");
                SelectFirstSequence(form);
                InvokePrivate(form, "RemoveSequence");
            }

            AssertEqual(0, repository.Load().Count, "Confirmed remove should delete saved sequence");
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

        private static void SequenceEditorForm_StoresConnectedDevicesForCompletion()
        {
            var devices = new[] { new RelayDevice("ABC123", RelayDeviceType.TwoChannel, 2, 0) };
            using (var form = new SequenceEditorForm(null, null, devices))
            {
                var storedDevices = (IReadOnlyList<RelayDevice>)GetPrivateField(form, "connectedDevices");

                AssertEqual(1, storedDevices.Count, "Editor connected device count");
                AssertEqual("ABC123", storedDevices[0].SerialNumber, "Editor connected device serial");
            }
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

        private static void RelayNamingRepository_RoundTripsFriendlyNames()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var repository = new RelayNamingRepository(path);
            var device = new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0, "hid#board-a");
            var configuration = new RelayNamingConfiguration();
            configuration.Devices.Add(new RelayDeviceNaming
            {
                DeviceKey = RelayNamingRepository.GetDeviceKey(device),
                Name = "DUT Power",
                Channels = new List<RelayChannelNaming>
                {
                    new RelayChannelNaming { Channel = 1, Name = "Main relay" }
                }
            });

            repository.Save(configuration);
            RelayDevice named = repository.Apply(new[] { device }).Single();

            AssertEqual("DUT Power", named.DeviceName, "Friendly device name");
            AssertEqual("Main relay", named.GetChannelName(1), "Friendly channel name");
            AssertEqual(1, named.ResolveChannel("main relay"), "Case-insensitive named channel lookup");
            AssertTrue(named.MatchesSelector("DUT Power"), "Friendly device selector");
        }

        private static void RelayService_RoutesByFriendlyDeviceAndChannelNames()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var device = new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0, "hid#board-a");
            var repository = new RelayNamingRepository(path);
            var configuration = new RelayNamingConfiguration();
            configuration.Devices.Add(new RelayDeviceNaming
            {
                DeviceKey = RelayNamingRepository.GetDeviceKey(device),
                Name = "DUT Power",
                Channels = new List<RelayChannelNaming>
                {
                    new RelayChannelNaming { Channel = 1, Name = "Main relay" }
                }
            });
            repository.Save(configuration);

            var fake = new FakeRelayBackend(device);
            var sequenceBackend = new RelaySequenceBackend(new RelayService(fake, repository));
            sequenceBackend.SetChannel("DUT Power", "Main relay", true);

            AssertTrue(fake.GetChannelState("6QMBS", 1), "Friendly device/channel should route to CH1");
        }

        private static void RelayService_UpdatesFriendlyNames()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var device = new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0, "hid#board-a");
            var repository = new RelayNamingRepository(path);
            var service = new RelayService(new FakeRelayBackend(device), repository);

            service.UpdateNames("hid#board-a", "DUT Power", new[] { "1", "Main relay", "2", "Debug" });
            RelayDevice named = service.EnumerateDevices().Single();

            AssertEqual("DUT Power", named.DeviceName, "Updated device name");
            AssertEqual("Main relay", named.GetChannelName(1), "Updated first channel name");
            AssertEqual("Debug", named.GetChannelName(2), "Updated second channel name");
        }

        private static void DeviceNamingForm_PopulatesExistingFriendlyNames()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var repository = new RelayNamingRepository(path);
            var device = new RelayDevice("6QMBS", RelayDeviceType.EightChannel, 8, 0, "hid#board-a");
            var configuration = new RelayNamingConfiguration();
            configuration.Devices.Add(new RelayDeviceNaming
            {
                DeviceKey = RelayNamingRepository.GetDeviceKey(device),
                Name = "DUT Power",
                Channels = new List<RelayChannelNaming>
                {
                    new RelayChannelNaming { Channel = 1, Name = "Main relay" }
                }
            });
            repository.Save(configuration);

            using (var form = new DeviceNamingForm(new[] { device }, repository))
            {
                var deviceNameEditor = (TextBox)GetPrivateField(form, "deviceNameEditor");
                var channelEditors = (Dictionary<int, TextBox>)GetPrivateField(form, "channelEditors");
                AssertTrue(form.GetType().GetProperty("Icon").GetValue(form, null) != null, "Naming form should use the application icon");
                AssertEqual("DUT Power", deviceNameEditor.Text, "Existing device name should populate");
                AssertEqual("Main relay", channelEditors[1].Text, "Existing channel name should populate");
            }
        }

        private static void DeviceNamingForm_PreservesSeparateDeviceValuesWhenSwitching()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var repository = new RelayNamingRepository(path);
            var first = new RelayDevice("FIRST", RelayDeviceType.TwoChannel, 2, 0, "hid#board-a");
            var second = new RelayDevice("SECOND", RelayDeviceType.TwoChannel, 2, 0, "hid#board-b");
            var configuration = new RelayNamingConfiguration();
            configuration.Devices.Add(new RelayDeviceNaming
            {
                DeviceKey = RelayNamingRepository.GetDeviceKey(first),
                Name = "First board",
                Channels = new List<RelayChannelNaming> { new RelayChannelNaming { Channel = 1, Name = "First channel" } }
            });
            configuration.Devices.Add(new RelayDeviceNaming
            {
                DeviceKey = RelayNamingRepository.GetDeviceKey(second),
                Name = "Second board",
                Channels = new List<RelayChannelNaming> { new RelayChannelNaming { Channel = 1, Name = "Second channel" } }
            });
            repository.Save(configuration);

            using (var form = new DeviceNamingForm(new[] { first, second }, repository))
            {
                var selector = (ComboBox)GetPrivateField(form, "deviceSelector");
                var deviceNameEditor = (TextBox)GetPrivateField(form, "deviceNameEditor");
                var channelEditors = (Dictionary<int, TextBox>)GetPrivateField(form, "channelEditors");
                deviceNameEditor.Text = "First board edited";
                selector.SelectedIndex = 1;

                AssertEqual("Second board", deviceNameEditor.Text, "Second device name should not copy first device name");
                AssertEqual("Second channel", channelEditors[1].Text, "Second channel name should not copy first device channel");

                selector.SelectedIndex = 0;
                AssertEqual("First board edited", deviceNameEditor.Text, "First device edit should be retained after switching back");
            }
        }

        private static void UsbRelayWrapper_StatusAlignsDevicePathColumn()
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "device-names.json");
            var repository = new RelayNamingRepository(path);
            var first = new RelayDevice(
                "6QMBS",
                RelayDeviceType.EightChannel,
                8,
                0,
                "hid#board-a",
                "power",
                new Dictionary<int, string> { { 1, "power" }, { 2, "dbg" } });
            var second = new RelayDevice("OTHER", RelayDeviceType.TwoChannel, 2, 0, "hid#board-b", "Buttons", null);
            Type wrapperType = typeof(MainForm).Assembly.GetType("usbrelay.UsbRelayWrapper", true);
            object wrapper = Activator.CreateInstance(
                wrapperType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { string.Empty, string.Empty, string.Empty, new RelayService(new FakeRelayBackend(first, second), repository) },
                null);
            var output = new StringWriter();
            TextWriter previous = Console.Out;
            try
            {
                Console.SetOut(output);
                wrapperType.GetMethod("status", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(wrapper, null);
            }
            finally
            {
                Console.SetOut(previous);
            }

            string[] lines = output.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            AssertTrue(lines.Length >= 4, "Status should contain header, separator, and device rows");
            AssertTrue(lines[0].Contains("Device Path"), "Status header should include Device Path");
            AssertEqual(lines[0].IndexOf("Device Path", StringComparison.Ordinal), lines[2].IndexOf("hid#board-a", StringComparison.Ordinal), "Device path column alignment");
            AssertEqual(lines[0].IndexOf("CH1", StringComparison.Ordinal), lines[2].IndexOf("power=OFF", StringComparison.Ordinal), "Channel column alignment");
        }

        private static void InvokePrivate(object instance, string methodName, params object[] arguments)
        {
            instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments);
        }

        private static void SelectFirstSequence(MainForm form)
        {
            var sequenceList = (DataGridView)GetPrivateField(form, "sequenceGrid");
            sequenceList.Rows[0].Selected = true;
            InvokePrivate(form, "SelectGridSequence");
        }

        private static SequenceCliHarness CreateSequenceCliHarness(FakeRelayBackend relay, params SequenceDefinition[] sequences)
        {
            return CreateSequenceCliHarness(relay, new StringWriter(), sequences);
        }

        private static SequenceCliHarness CreateSequenceCliHarness(FakeRelayBackend relay, TextWriter output, params SequenceDefinition[] sequences)
        {
            string path = Path.Combine(Path.GetTempPath(), "usbrelay-tests-" + Guid.NewGuid().ToString("N"), "sequences.json");
            var repository = new SequenceRepository(path);
            repository.Save(sequences);
            var error = new StringWriter();
            return new SequenceCliHarness(
                new SequenceCli(repository, relay, new FakeExternalToolRunner(string.Empty), output, error),
                output,
                error);
        }

        private static SequenceCliHarness CreateSequenceCliHarness(RelayDevice device, params SequenceDefinition[] sequences)
        {
            return CreateSequenceCliHarness(new FakeRelayBackend(device), sequences);
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

        private static bool ShouldWriteInteractiveSequenceSeparator(string command, bool outputRedirected)
        {
            var cliType = typeof(MainForm).Assembly.GetType("usbrelay.UsbRelayCli", true);
            var method = cliType.GetMethod("ShouldWriteInteractiveSequenceSeparator", BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(method != null, "UsbRelayCli.ShouldWriteInteractiveSequenceSeparator should exist");
            return (bool)method.Invoke(null, new object[] { command, outputRedirected });
        }

        private static Encoding CreateConsoleStreamEncoding(Encoding encoding)
        {
            var consoleWindowType = typeof(MainForm).Assembly.GetType("usbrelay.ConsoleWindow", true);
            var method = consoleWindowType.GetMethod("CreateConsoleStreamEncoding", BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(method != null, "ConsoleWindow.CreateConsoleStreamEncoding should exist");
            return (Encoding)method.Invoke(null, new object[] { encoding });
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
            var document = new XmlDocument();
            document.Load(path);

            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003");

            XmlNode node = document.SelectSingleNode("/msb:Project/msb:PropertyGroup/msb:" + propertyName, namespaceManager);
            if (node == null || string.IsNullOrWhiteSpace(node.InnerText))
                throw new InvalidOperationException("Property " + propertyName + " was not found in " + path);

            return node.InnerText.Trim();
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
            return RunProcess(executablePath, args);
        }

        private static ProcessResult RunProcess(string executablePath, params string[] args)
        {
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

        private sealed class SequenceCliHarness
        {
            public SequenceCliHarness(SequenceCli cli, TextWriter output, TextWriter error)
            {
                Cli = cli;
                Output = output;
                Error = error;
            }

            public SequenceCli Cli { get; }
            public TextWriter Output { get; }
            public TextWriter Error { get; }
        }

        private sealed class RecordingTextWriter : TextWriter
        {
            private readonly StringBuilder builder = new StringBuilder();

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public int WriteCallCount { get; private set; }

            public override void Write(bool value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(char value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(char[] buffer)
            {
                RecordWrite(buffer == null ? null : new string(buffer));
            }

            public override void Write(char[] buffer, int index, int count)
            {
                RecordWrite(buffer == null ? null : new string(buffer, index, count));
            }

            public override void Write(decimal value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(double value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(float value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(int value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(long value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(object value)
            {
                RecordWrite(value == null ? null : value.ToString());
            }

            public override void Write(string value)
            {
                RecordWrite(value);
            }

            public override void Write(string format, object arg0)
            {
                RecordWrite(string.Format(format, arg0));
            }

            public override void Write(string format, object arg0, object arg1)
            {
                RecordWrite(string.Format(format, arg0, arg1));
            }

            public override void Write(string format, object arg0, object arg1, object arg2)
            {
                RecordWrite(string.Format(format, arg0, arg1, arg2));
            }

            public override void Write(string format, params object[] arg)
            {
                RecordWrite(string.Format(format, arg));
            }

            public override void Write(uint value)
            {
                RecordWrite(value.ToString());
            }

            public override void Write(ulong value)
            {
                RecordWrite(value.ToString());
            }

            public override void WriteLine()
            {
                RecordWrite(Environment.NewLine);
            }

            public override void WriteLine(bool value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(char value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(char[] buffer)
            {
                RecordWrite((buffer == null ? null : new string(buffer)) + Environment.NewLine);
            }

            public override void WriteLine(char[] buffer, int index, int count)
            {
                RecordWrite((buffer == null ? null : new string(buffer, index, count)) + Environment.NewLine);
            }

            public override void WriteLine(decimal value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(double value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(float value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(int value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(long value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(object value)
            {
                RecordWrite((value == null ? null : value.ToString()) + Environment.NewLine);
            }

            public override void WriteLine(string value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(string format, object arg0)
            {
                RecordWrite(string.Format(format, arg0) + Environment.NewLine);
            }

            public override void WriteLine(string format, object arg0, object arg1)
            {
                RecordWrite(string.Format(format, arg0, arg1) + Environment.NewLine);
            }

            public override void WriteLine(string format, object arg0, object arg1, object arg2)
            {
                RecordWrite(string.Format(format, arg0, arg1, arg2) + Environment.NewLine);
            }

            public override void WriteLine(string format, params object[] arg)
            {
                RecordWrite(string.Format(format, arg) + Environment.NewLine);
            }

            public override void WriteLine(uint value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override void WriteLine(ulong value)
            {
                RecordWrite(value + Environment.NewLine);
            }

            public override string ToString()
            {
                return builder.ToString();
            }

            private void RecordWrite(string value)
            {
                WriteCallCount++;
                builder.Append(value);
            }
        }

        private sealed class ThrowingRelayBackend : IRelayBackend
        {
            private readonly Exception exception;

            public ThrowingRelayBackend(Exception exception)
            {
                this.exception = exception;
            }

            public IReadOnlyList<RelayDevice> EnumerateDevices()
            {
                throw exception;
            }

            public RelayDevice GetDevice(string serialNumber)
            {
                throw exception;
            }

            public void SetChannel(string serialNumber, int channel, bool on)
            {
                throw exception;
            }

            public void SetChannel(RelayDevice device, int channel, bool on)
            {
                throw exception;
            }

            public void SetChannel(string deviceSelector, string channelName, bool on)
            {
                throw exception;
            }

            public bool GetChannelState(string serialNumber, int channel)
            {
                throw exception;
            }

            public bool GetChannelState(string deviceSelector, string channelName)
            {
                throw exception;
            }
        }
    }
}
