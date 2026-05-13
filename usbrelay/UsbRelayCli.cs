using System;
using System.Collections.Generic;
using System.Linq;

namespace usbrelay
{
    internal static class UsbRelayCli
    {
        public static int Run(string[] args)
        {
            if (IsCompletionRequest(args))
                return RunCompletion(args);

            if (IsSequenceRequest(args))
                return RunSequenceCommand(args);

            if (args.Length < 1)
            {
                PrintHelpWithExamples();
                return 0;
            }

            ParsedCliCommand command = ParseCommand(args);
            if (command.IsHelpRequested)
            {
                PrintHelpWithExamples();
                return 0;
            }

            if (command.HasSystemCommandLineErrors || command.IsVersionRequested)
                return InvokeSystemCommandLine(args);

            if (!command.IsValid)
            {
                foreach (string error in command.Errors)
                    Console.Error.WriteLine(error);
                return 1;
            }

            UsbRelayWrapper control = new UsbRelayWrapper(command.Serial);
            switch (command.Operation)
            {
                case Operations.LIST:
                    control.list();
                    break;
                case Operations.STATUS:
                    control.status();
                    break;
                case Operations.ONOFF:
                    control.on_off_channels(new HashSet<int>(command.OnChannels), new HashSet<int>(command.OffChannels));
                    break;
            }

            return 0;
        }

        internal static ParsedCliCommand ParseCommand(string[] args)
        {
            string[] normalizedArgs = NormalizeArguments(args);
            CliGrammar grammar = CliGrammar.Current;
            var parseResult = grammar.RootCommand.Parse(normalizedArgs);
            var errors = parseResult.Errors.Select(error => error.Message).ToList();
            bool hasSystemCommandLineErrors = errors.Count > 0;
            bool isHelpRequested = normalizedArgs.Any(IsHelpArgument);
            bool isVersionRequested = normalizedArgs.Any(IsVersionArgument);
            bool isGui = false;
            bool list = false;
            bool status = false;
            string serial = string.Empty;
            int[] onChannels = new int[0];
            int[] offChannels = new int[0];
            Operations operation = Operations.NULL;

            if (!hasSystemCommandLineErrors)
            {
                isGui = parseResult.GetValue(grammar.GuiOption) || parseResult.GetValue(grammar.LegacyGuiOption);
                list = parseResult.GetValue(grammar.ListOption) || parseResult.GetValue(grammar.LegacyListOption);
                status = parseResult.GetValue(grammar.StatusOption) || parseResult.GetValue(grammar.LegacyStatusOption);
                serial = FirstNonEmpty(parseResult.GetValue(grammar.SerialOption), parseResult.GetValue(grammar.LegacySerialOption));
                onChannels = CombineChannels(parseResult.GetValue(grammar.OnOption), parseResult.GetValue(grammar.LegacyOnOption));
                offChannels = CombineChannels(parseResult.GetValue(grammar.OffOption), parseResult.GetValue(grammar.LegacyOffOption));

                if (list)
                    operation = Operations.LIST;
                else if (status)
                    operation = Operations.STATUS;
                else if (onChannels.Length > 0 || offChannels.Length > 0)
                    operation = Operations.ONOFF;

                bool hasRelayOptions = list || status || !string.IsNullOrEmpty(serial) || onChannels.Length > 0 || offChannels.Length > 0;
                if (isGui && hasRelayOptions)
                    errors.Add("--gui cannot be combined with relay operation options.");
            }

            return new ParsedCliCommand(
                operation,
                serial,
                onChannels,
                offChannels,
                isGui,
                isHelpRequested,
                isVersionRequested,
                hasSystemCommandLineErrors,
                errors);
        }

        private static bool IsCompletionRequest(string[] args)
        {
            return args.Length > 0 && string.Equals(args[0], "complete", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSequenceRequest(string[] args)
        {
            return args.Length > 0 && string.Equals(args[0], "sequence", StringComparison.OrdinalIgnoreCase);
        }

        private static int RunSequenceCommand(string[] args)
        {
            if (args.Any(IsHelpArgument))
                return InvokeSystemCommandLine(args);

            CliGrammar grammar = CliGrammar.Current;
            var parseResult = grammar.RootCommand.Parse(NormalizeArguments(args));
            if (parseResult.Errors.Count > 0)
            {
                foreach (var error in parseResult.Errors)
                    Console.Error.WriteLine(error.Message);
                return 1;
            }

            var sequences = new SequenceCli();
            var command = parseResult.CommandResult.Command;
            string commandName = command.Name;
            if (ReferenceEquals(command, grammar.SequenceQueryCommand))
            {
                string name = parseResult.GetValue(grammar.SequenceQueryNameOption);
                return sequences.Query(name, ShouldWriteInteractiveSequenceSeparator(commandName, Console.IsOutputRedirected));
            }

            if (ReferenceEquals(command, grammar.SequenceStatusCommand))
            {
                string name = parseResult.GetValue(grammar.SequenceStatusNameOption);
                return sequences.Status(name, ShouldWriteInteractiveSequenceSeparator(commandName, Console.IsOutputRedirected));
            }

            if (ReferenceEquals(command, grammar.SequenceRunCommand))
            {
                string name = parseResult.GetValue(grammar.SequenceRunNameOption);
                return sequences.Run(name, ShouldWriteInteractiveSequenceSeparator(commandName, Console.IsOutputRedirected));
            }

            Console.Error.WriteLine("Usage: usbrelay sequence <query|status|run> [--name <name>]");
            return 1;
        }

        private static bool ShouldWriteInteractiveSequenceSeparator(string command, bool outputRedirected)
        {
            if (outputRedirected)
                return false;

            return string.Equals(command, "query", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "status", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "run", StringComparison.OrdinalIgnoreCase);
        }

        private static int RunCompletion(string[] args)
        {
            string line;
            int position;
            if (!TryReadCompletionArguments(args, out line, out position))
            {
                Console.Error.WriteLine("Usage: usbrelay complete --position <cursor> --line <command line>");
                return 1;
            }

            foreach (string completion in CliCompletion.GetSuggestions(line, position))
                Console.WriteLine(completion);

            return 0;
        }

        private static void PrintHelpWithExamples()
        {
            InvokeSystemCommandLine(new[] { "--help" });
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  usbrelay --list");
            Console.WriteLine("  usbrelay --status");
            Console.WriteLine("  usbrelay --serial BITFT --on 1");
            Console.WriteLine("  usbrelay --serial BITFT --on 1 2 3");
            Console.WriteLine("  usbrelay --serial BITFT --off 2");
            Console.WriteLine("  usbrelay --serial BITFT --on 1 3 5 --off 2 4 6");
            Console.WriteLine("  usbrelay --gui");
            Console.WriteLine("  usbrelay sequence query");
            Console.WriteLine("  usbrelay sequence status --name \"Power cycle DUT\"");
            Console.WriteLine("  usbrelay sequence run --name \"Power cycle DUT\"");
        }

        private static int InvokeSystemCommandLine(string[] args)
        {
            return CliGrammar.Current.RootCommand.Parse(NormalizeArguments(args)).Invoke();
        }

        private static bool TryReadCompletionArguments(string[] args, out string line, out int position)
        {
            line = null;
            position = -1;
            for (int i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--line", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    line = args[++i];
                    continue;
                }

                if (string.Equals(args[i], "--position", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], out position))
                        return false;
                }
            }

            return line != null && position >= 0;
        }

        private static string[] NormalizeArguments(string[] args)
        {
            return args.Select(arg => string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase) ? "--version" : arg).ToArray();
        }

        private static bool IsHelpArgument(string arg)
        {
            return string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-?", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVersionArgument(string arg)
        {
            return string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return string.Empty;
        }

        private static int[] CombineChannels(params int[][] channelGroups)
        {
            return channelGroups
                .Where(channels => channels != null)
                .SelectMany(channels => channels)
                .ToArray();
        }
    }
}
