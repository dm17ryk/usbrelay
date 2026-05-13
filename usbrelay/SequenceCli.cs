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
        private readonly SequenceRepository repository;
        private readonly IRelayBackend relayBackend;
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
                output.WriteLine("No saved sequences.");
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
                devices = relayBackend.EnumerateDevices();
            }
            catch (Exception ex)
            {
                error.WriteLine("Failed to enumerate relay devices: " + ex.Message);
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
                new RelaySequenceBackend(relayBackend),
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
            IReadOnlyList<SequenceDefinition> sequences = repository.Load();
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
            return devices.Any(device =>
                string.Equals(device.SerialNumber, resource.SerialNumber, StringComparison.OrdinalIgnoreCase) &&
                resource.Channel >= 1 &&
                resource.Channel <= device.ChannelCount);
        }

        private static string FormatResources(IEnumerable<RelayResource> resources)
        {
            var values = resources
                .Select(resource => resource.SerialNumber + ":CH" + resource.Channel)
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
