using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using usbrelay.Sequences;

namespace usbrelay
{
    public sealed class SequenceCli
    {
        private static readonly string[] FunctionReference =
        {
            "Sequence functions:",
            "  sequence.PowerOn(\"device\", channelOrName);",
            "  sequence.PowerOff(\"device\", channelOrName);",
            "  sequence.ReadChannel(\"device\", channelOrName);",
            "  sequence.WaitChannel(\"device\", channelOrName, RelayState.On|Off, timeoutMs);",
            "  sequence.Sleep(milliseconds);",
            "  var tool = sequence.RunTool(\"tool.exe\", \"arguments\");",
            "  sequence.Fail(\"message\");",
            "",
            "Operators and control constructs:",
            "  if (tool.OutputMatches(\"regex\")) { ... } else { ... }",
            "  var <name> = sequence.RunTool(...);",
            "  RelayState.On and RelayState.Off",
            "",
            "Device and channel selectors may be serial numbers, full device paths,",
            "friendly device names, numeric channels, or configured channel names."
        };

        private readonly SequenceRepository repository;
        private readonly IRelayBackend relayBackend;
        private readonly RelayService relayService;
        private readonly IExternalToolRunner toolRunner;
        private readonly TextWriter output;
        private readonly TextWriter error;

        public SequenceCli()
            : this(
                new SequenceRepository(SequenceRepository.DefaultPath),
                new NativeUsbRelayBackend(),
                new ProcessExternalToolRunner(),
                Console.Out,
                Console.Error)
        {
        }

        public SequenceCli(
            SequenceRepository repository,
            IRelayBackend relayBackend,
            IExternalToolRunner toolRunner,
            TextWriter output,
            TextWriter error)
        {
            this.repository = repository;
            this.relayBackend = relayBackend;
            relayService = new RelayService(relayBackend);
            this.toolRunner = toolRunner;
            this.output = output;
            this.error = error;
        }

        public int Query(string name)
        {
            return Query(name, startOnCleanLine: false);
        }

        public int Query(string name, bool startOnCleanLine)
        {
            IReadOnlyList<SequenceDefinition> sequences;
            if (!TrySelectSequences(name, out sequences))
                return 1;

            if (sequences.Count == 0)
            {
                output.Write(PrefixCleanLine("No saved sequences." + Environment.NewLine, startOnCleanLine));
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                output.Write(PrefixCleanLine(BuildDetails(sequences[0]), startOnCleanLine));
                return 0;
            }

            var rows = new List<string[]>();
            foreach (SequenceDefinition sequence in sequences)
            {
                SequenceParseResult parsed = SequenceParser.Parse(sequence.Script);
                rows.Add(new[]
                {
                    Display(sequence.Name),
                    Display(sequence.DisplayRunButtonText),
                    parsed.IsValid ? "Valid" : "Invalid",
                    FormatResources(parsed.Resources)
                });
            }

            string text = BuildTable(
                new[] { "Name", "Run button", "Validity", "Resources" },
                rows);
            output.Write(PrefixCleanLine(text, startOnCleanLine));
            return 0;
        }

        public int Add(string name, string script, string scriptFile, string runButton, string description)
        {
            return Add(name, script, scriptFile, runButton, description, startOnCleanLine: false);
        }

        public int Add(string name, string script, string scriptFile, string runButton, string description, bool startOnCleanLine)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                error.WriteLine("Sequence name is required. Use --name <name>.");
                return 1;
            }

            string resolvedScript = null;
            if (!TryReadScript(script, scriptFile, out resolvedScript))
                return 1;

            List<SequenceDefinition> sequences;
            if (!TryLoadSequences(out sequences))
                return 1;

            string trimmedName = name.Trim();
            if (sequences.Any(sequence => string.Equals(sequence.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                error.WriteLine("Duplicate sequence name: " + trimmedName);
                return 1;
            }

            sequences.Add(new SequenceDefinition
            {
                Name = trimmedName,
                RunButtonText = runButton,
                Description = description ?? string.Empty,
                Script = resolvedScript
            });
            repository.Save(sequences);
            output.Write(PrefixCleanLine("Added sequence: " + trimmedName + Environment.NewLine, startOnCleanLine));
            return 0;
        }

        public int Read(string name)
        {
            return Read(name, startOnCleanLine: false);
        }

        public int Read(string name, bool startOnCleanLine)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                error.WriteLine("Sequence name is required. Use --name <name>.");
                return 1;
            }

            return Query(name, startOnCleanLine);
        }

        public int Modify(string name, string newName, string script, string scriptFile, string runButton, string description)
        {
            return Modify(name, newName, script, scriptFile, runButton, description, startOnCleanLine: false);
        }

        public int Modify(string name, string newName, string script, string scriptFile, string runButton, string description, bool startOnCleanLine)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                error.WriteLine("Existing sequence name is required. Use --name <name>.");
                return 1;
            }

            if (script != null && scriptFile != null)
            {
                error.WriteLine("Use only one of --script or --script-file.");
                return 1;
            }

            if (newName == null && script == null && scriptFile == null && runButton == null && description == null)
            {
                error.WriteLine("No sequence changes were supplied.");
                return 1;
            }

            List<SequenceDefinition> sequences;
            if (!TryLoadSequences(out sequences))
                return 1;

            string trimmedName = name.Trim();
            var matches = sequences
                .Where(sequence => string.Equals(sequence.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0)
            {
                error.WriteLine("Sequence not found: " + trimmedName);
                return 1;
            }

            if (matches.Length > 1)
            {
                error.WriteLine("Duplicate sequence name: " + trimmedName);
                return 1;
            }

            SequenceDefinition sequenceToModify = matches[0];
            string resolvedScript = null;
            if ((script != null || scriptFile != null) && !TryReadScript(script, scriptFile, out resolvedScript))
                return 1;

            if (newName != null)
            {
                if (string.IsNullOrWhiteSpace(newName))
                {
                    error.WriteLine("New sequence name cannot be empty.");
                    return 1;
                }

                string trimmedNewName = newName.Trim();
                if (sequences.Any(sequence => !ReferenceEquals(sequence, sequenceToModify)
                    && string.Equals(sequence.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase)))
                {
                    error.WriteLine("Duplicate sequence name: " + trimmedNewName);
                    return 1;
                }

                sequenceToModify.Name = trimmedNewName;
            }

            if (script != null || scriptFile != null)
                sequenceToModify.Script = resolvedScript;
            if (runButton != null)
                sequenceToModify.RunButtonText = runButton;
            if (description != null)
                sequenceToModify.Description = description;

            repository.Save(sequences);
            output.Write(PrefixCleanLine("Updated sequence: " + Display(sequenceToModify.Name) + Environment.NewLine, startOnCleanLine));
            return 0;
        }

        public int Functions()
        {
            return Functions(startOnCleanLine: false);
        }

        public int Functions(bool startOnCleanLine)
        {
            output.Write(PrefixCleanLine(string.Join(Environment.NewLine, FunctionReference) + Environment.NewLine, startOnCleanLine));
            return 0;
        }

        public int Status(string name)
        {
            return Status(name, startOnCleanLine: false);
        }

        public int Status(string name, bool startOnCleanLine)
        {
            IReadOnlyList<SequenceDefinition> sequences;
            if (!TrySelectSequences(name, out sequences))
                return 1;

            if (sequences.Count == 0)
            {
                output.Write(PrefixCleanLine("No saved sequences." + Environment.NewLine, startOnCleanLine));
                return 0;
            }

            IReadOnlyList<RelayDevice> devices;
            try
            {
                devices = relayService.EnumerateDevices();
            }
            catch (Exception ex)
            {
                WriteException("Failed to enumerate relay devices.", ex);
                return 1;
            }

            bool allReady = true;
            var builder = new StringBuilder();
            foreach (SequenceDefinition sequence in sequences)
            {
                SequenceParseResult parsed = SequenceParser.Parse(sequence.Script);
                if (!parsed.IsValid)
                {
                    allReady = false;
                    builder.AppendLine(Display(sequence.Name) + ": Invalid");
                    foreach (string diagnostic in parsed.Diagnostics)
                        builder.AppendLine("  " + diagnostic);
                    continue;
                }

                var missing = parsed.Resources.Where(resource => !HasResource(devices, resource)).ToArray();
                if (missing.Length > 0)
                {
                    allReady = false;
                    builder.AppendLine(Display(sequence.Name) + ": Missing resources " + FormatResources(missing));
                    continue;
                }

                builder.AppendLine(Display(sequence.Name) + ": Ready " + FormatResources(parsed.Resources));
            }

            output.Write(PrefixCleanLine(builder.ToString(), startOnCleanLine));
            return allReady ? 0 : 1;
        }

        public int Run(string name)
        {
            return Run(name, startOnCleanLine: false);
        }

        public int Run(string name, bool startOnCleanLine)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                error.WriteLine("Sequence name is required. Use --name <name>.");
                return 1;
            }

            IReadOnlyList<SequenceDefinition> sequences;
            if (!TrySelectSequences(name, out sequences))
                return 1;

            SequenceDefinition sequence = sequences[0];
            SequenceParseResult parsed = SequenceParser.Parse(sequence.Script);
            if (!parsed.IsValid)
            {
                foreach (string diagnostic in parsed.Diagnostics)
                    error.WriteLine(diagnostic);
                return 1;
            }

            output.Write(PrefixCleanLine(Display(sequence.Name) + " started" + Environment.NewLine, startOnCleanLine));
            SequenceRunResult result = SequenceRunner.Run(
                parsed,
                new RelaySequenceBackend(relayService),
                toolRunner,
                skipDelays: false);

            foreach (string line in result.Log)
                output.WriteLine(line);

            output.WriteLine(Display(sequence.Name) + (result.Success ? " finished" : " failed"));
            if (!result.Success && result.Error != null)
                error.WriteLine(result.Error.Message);

            return result.Success ? 0 : 1;
        }

        private bool TrySelectSequences(string name, out IReadOnlyList<SequenceDefinition> selected)
        {
            IReadOnlyList<SequenceDefinition> sequences;
            try
            {
                sequences = repository.Load();
            }
            catch (Exception ex)
            {
                selected = new SequenceDefinition[0];
                WriteException("Failed to load sequences from repository.", ex);
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                selected = sequences;
                return true;
            }

            var matches = sequences
                .Where(sequence => string.Equals(sequence.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length == 0)
            {
                selected = new SequenceDefinition[0];
                error.WriteLine("Sequence not found: " + name);
                return false;
            }

            if (matches.Length > 1)
            {
                selected = new SequenceDefinition[0];
                error.WriteLine("Duplicate sequence name: " + name);
                return false;
            }

            selected = matches;
            return true;
        }

        private bool TryLoadSequences(out List<SequenceDefinition> sequences)
        {
            try
            {
                sequences = repository.Load().ToList();
                return true;
            }
            catch (Exception ex)
            {
                sequences = new List<SequenceDefinition>();
                WriteException("Failed to load sequences from repository.", ex);
                return false;
            }
        }

        private bool TryReadScript(string script, string scriptFile, out string resolvedScript)
        {
            resolvedScript = script;
            if (script != null && scriptFile != null)
            {
                error.WriteLine("Use only one of --script or --script-file.");
                return false;
            }

            if (scriptFile == null)
            {
                if (script == null)
                {
                    error.WriteLine("A sequence script is required. Use --script or --script-file.");
                    return false;
                }

                return true;
            }

            try
            {
                resolvedScript = File.ReadAllText(scriptFile);
                return true;
            }
            catch (Exception ex)
            {
                WriteException("Failed to read sequence script file.", ex);
                return false;
            }
        }

        private void WriteException(string message, Exception ex)
        {
            error.WriteLine(message + " " + ex.Message + " (" + ex.GetType().FullName + ")");
            error.WriteLine(ex.ToString());
        }

        private static string BuildDetails(SequenceDefinition sequence)
        {
            SequenceParseResult parsed = SequenceParser.Parse(sequence.Script);
            var builder = new StringBuilder();
            builder.AppendLine("Name: " + Display(sequence.Name));
            builder.AppendLine("Run button: " + Display(sequence.DisplayRunButtonText));
            builder.AppendLine("Description: " + Display(sequence.Description));
            builder.AppendLine("Valid: " + (parsed.IsValid ? "Yes" : "No"));
            builder.AppendLine("Resources: " + FormatResources(parsed.Resources));
            if (parsed.Diagnostics.Count == 0)
                builder.AppendLine("Diagnostics: none");
            else
            {
                builder.AppendLine("Diagnostics:");
                foreach (string diagnostic in parsed.Diagnostics)
                    builder.AppendLine("  " + diagnostic);
            }

            builder.AppendLine("Script:");
            builder.AppendLine(sequence.Script ?? string.Empty);
            return builder.ToString();
        }

        private static string PrefixCleanLine(string text, bool startOnCleanLine)
        {
            return startOnCleanLine ? Environment.NewLine + text : text;
        }

        private static string BuildTable(string[] headers, IEnumerable<string[]> rows)
        {
            var rowArray = rows.ToArray();
            int[] widths = headers.Select(header => header.Length).ToArray();
            foreach (string[] row in rowArray)
            {
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = Math.Max(widths[i], row[i].Length);
            }

            var builder = new StringBuilder();
            AppendTableLine(builder, headers, widths);
            AppendTableLine(builder, widths.Select(width => new string('-', width)).ToArray(), widths);
            foreach (string[] row in rowArray)
                AppendTableLine(builder, row, widths);
            return builder.ToString();
        }

        private static void AppendTableLine(StringBuilder builder, string[] values, int[] widths)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    builder.Append("  ");
                builder.Append(values[i].PadRight(widths[i]));
            }

            builder.AppendLine();
        }

        private static bool HasResource(IEnumerable<RelayDevice> devices, RelayResource resource)
        {
            var matches = devices
                .Where(device => device.MatchesSelector(resource.SerialNumber))
                .ToArray();
            if (matches.Length != 1)
                return false;

            int channel;
            try
            {
                channel = string.IsNullOrEmpty(resource.ChannelName)
                    ? resource.Channel
                    : matches[0].ResolveChannel(resource.ChannelName);
            }
            catch (Exception)
            {
                return false;
            }

            return channel >= 1 && channel <= matches[0].ChannelCount;
        }

        private static string FormatResources(IEnumerable<RelayResource> resources)
        {
            var values = resources
                .Select(resource => resource.SerialNumber + ":" + (string.IsNullOrEmpty(resource.ChannelName) ? "CH" + resource.Channel : resource.ChannelName))
                .OrderBy(value => value)
                .ToArray();
            return values.Length == 0 ? "-" : string.Join(", ", values);
        }

        private static string Display(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
