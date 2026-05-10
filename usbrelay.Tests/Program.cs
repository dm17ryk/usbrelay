using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using usbrelay.Sequences;

namespace usbrelay.Tests
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            var tests = new Action[]
            {
                SequenceRepository_RoundTripsSequencesAsJson,
                SequenceParser_ParsesDslAndResources,
                SequenceResourceLocks_BlockOverlappingChannelsOnly,
                SequenceRunner_ExecutesRegexSuccessBranchWithFakeRelayAndTool,
                MainForm_LoadsSavedSequencesIntoVisibleRows
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
                var sequenceList = (FlowLayoutPanel)GetPrivateField(form, "sequenceListPanel");

                AssertEqual(1, sequenceList.Controls.Count, "Visible sequence row count");
                AssertTrue(sequenceList.Controls[0].Controls["sequenceNameButton"].Text == "Visible sequence", "Sequence name button should be visible");
                AssertTrue(sequenceList.Controls[0].Controls["sequenceRunButton"].Text == "Run", "Sequence run button should be visible");
            }
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
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
    }
}
