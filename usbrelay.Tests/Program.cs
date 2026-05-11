using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
    }
}
