//  
//  USB-Relay Utility
//      - A generic tool to handle common 1-8 channel USB-Relay boards.
//      - Developd for Astro-Photograpy Equipment Power Control.
//
//  Author: Min Xie (minxie.dallas@gmail.com)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Completions;
using System.Windows.Forms;

namespace usbrelay
{
    class Program
    {
        enum Operations { NULL, LIST, STATUS, ONOFF };
        enum StartupMode { Cli, Gui };
        static readonly string[] ChannelCompletionValues = Enumerable.Range(1, 8).Select(channel => channel.ToString()).ToArray();
        static readonly string[] CliCompletionOptions =
        {
            "--list",
            "-list",
            "--status",
            "-status",
            "--serial",
            "-serial",
            "--on",
            "-on",
            "--off",
            "-off",
            "--gui",
            "-gui",
            "-?",
            "-h",
            "--help",
            "--version",
            "-v"
        };

        [STAThread]
        static int Main(string[] args)
        {
            if (SelectStartupMode(args, ConsoleWindow.HasInheritedConsole()) == StartupMode.Gui)
                return RunGui();

            return RunCli(args);
        }

        static StartupMode SelectStartupMode(string[] args, bool hasInheritedConsole)
        {
            ParsedCliCommand command = ParseCliCommand(args);
            if (command.IsValid && command.IsGui && !command.HasRelayOptions)
                return StartupMode.Gui;

            if (args.Length == 0 && !hasInheritedConsole)
                return StartupMode.Gui;

            return StartupMode.Cli;
        }

        static bool IsGuiArgument(string arg)
        {
            return string.Equals(arg, "--gui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-gui", StringComparison.OrdinalIgnoreCase);
        }

        static int RunGui()
        {
            ConsoleWindow.HideForGui();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm mainForm;
            using (var splash = new SplashForm())
            {
                splash.Show();
                splash.SetStatus("Discovering USB relay devices...");

                mainForm = new MainForm();
                mainForm.PrepareForDisplay();

                splash.Close();
            }
            Application.Run(mainForm);
            return 0;
        }

        static int RunCli(string[] args)
        {
            if (IsCompletionRequest(args))
                return RunCompletion(args);

            if (args.Length < 1)
            {
                PrintHelpWithExamples();
                return 0;
            }

            ParsedCliCommand command = ParseCliCommand(args);
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

            // process commands
            UsbRelayWrapper control = new UsbRelayWrapper(command.Serial);
            switch(command.Operation)
            {
                case Operations.LIST: control.list(); break;
                case Operations.STATUS: control.status(); break;
                case Operations.ONOFF: control.on_off_channels(new HashSet<int>(command.OnChannels), new HashSet<int>(command.OffChannels)); break;
                default: break;
            }

            return 0;
        }

        static bool IsCompletionRequest(string[] args)
        {
            return args.Length > 0 && string.Equals(args[0], "complete", StringComparison.OrdinalIgnoreCase);
        }

        static int RunCompletion(string[] args)
        {
            string line;
            int position;
            if (!TryReadCompletionArguments(args, out line, out position))
            {
                Console.Error.WriteLine("Usage: usbrelay complete --position <cursor> --line <command line>");
                return 1;
            }

            foreach (string completion in GetCompletionSuggestions(line, position))
                Console.WriteLine(completion);

            return 0;
        }

        static void PrintHelpWithExamples()
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
        }

        static int InvokeSystemCommandLine(string[] args)
        {
            CliGrammar grammar = CreateCliGrammar();
            return grammar.RootCommand.Parse(NormalizeArguments(args)).Invoke();
        }

        static bool TryReadCompletionArguments(string[] args, out string line, out int position)
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

        static IEnumerable<string> GetCompletionSuggestions(string commandLine, int cursorPosition)
        {
            if (commandLine == null)
                commandLine = string.Empty;

            if (cursorPosition < 0)
                cursorPosition = 0;
            if (cursorPosition > commandLine.Length)
                cursorPosition = commandLine.Length;

            string prefix = commandLine.Substring(0, cursorPosition);
            bool endsWithWhitespace;
            List<string> tokens = SplitCommandLine(prefix, out endsWithWhitespace);
            RemoveCommandToken(tokens);

            string wordToComplete = endsWithWhitespace || tokens.Count == 0 ? string.Empty : tokens[tokens.Count - 1];
            List<string> previousTokens = endsWithWhitespace
                ? tokens
                : tokens.Take(Math.Max(0, tokens.Count - 1)).ToList();

            if (IsCompletingChannelValue(previousTokens, wordToComplete))
                return ChannelCompletionValues.Where(value => StartsWith(value, wordToComplete));

            if (wordToComplete.Length == 0 || wordToComplete.StartsWith("-", StringComparison.Ordinal))
                return CliCompletionOptions.Where(option => StartsWith(option, wordToComplete)).Distinct().OrderBy(option => option);

            return Enumerable.Empty<string>();
        }

        static bool IsCompletingChannelValue(IEnumerable<string> previousTokens, string wordToComplete)
        {
            if (wordToComplete.StartsWith("-", StringComparison.Ordinal))
                return false;

            foreach (string token in previousTokens.Reverse())
            {
                if (IsChannelOption(token))
                    return true;

                if (IsOptionToken(token))
                    return false;
            }

            return false;
        }

        static bool IsChannelOption(string token)
        {
            return string.Equals(token, "--on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "-on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "--off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "-off", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsOptionToken(string token)
        {
            return token.StartsWith("-", StringComparison.Ordinal);
        }

        static bool StartsWith(string value, string prefix)
        {
            return value.StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        static void RemoveCommandToken(List<string> tokens)
        {
            if (tokens.Count == 0)
                return;

            string token = tokens[0];
            if (!token.StartsWith("-", StringComparison.Ordinal))
                tokens.RemoveAt(0);
        }

        static List<string> SplitCommandLine(string commandLine, out bool endsWithWhitespace)
        {
            endsWithWhitespace = commandLine.Length > 0 && char.IsWhiteSpace(commandLine[commandLine.Length - 1]);
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (char ch in commandLine)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Length = 0;
                    }
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }

        static ParsedCliCommand ParseCliCommand(string[] args)
        {
            string[] normalizedArgs = NormalizeArguments(args);
            CliGrammar grammar = CreateCliGrammar();
            ParseResult parseResult = grammar.RootCommand.Parse(normalizedArgs);
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

        static string[] NormalizeArguments(string[] args)
        {
            return args.Select(arg => string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase) ? "--version" : arg).ToArray();
        }

        static bool IsHelpArgument(string arg)
        {
            return string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-?", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsVersionArgument(string arg)
        {
            return string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase);
        }

        static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return string.Empty;
        }

        static int[] CombineChannels(params int[][] channelGroups)
        {
            return channelGroups
                .Where(channels => channels != null)
                .SelectMany(channels => channels)
                .ToArray();
        }

        static CliGrammar CreateCliGrammar()
        {
            var listOption = CreateBoolOption("--list", "List all available serial numbers of connected USB relay devices.");
            var legacyListOption = CreateHiddenBoolOption("-list");
            var statusOption = CreateBoolOption("--status", "Display relay channel status for connected USB relay devices.");
            var legacyStatusOption = CreateHiddenBoolOption("-status");
            var serialOption = new Option<string>("--serial")
            {
                Description = "Specify the serial number of the USB relay device to operate."
            };
            var legacySerialOption = new Option<string>("-serial")
            {
                Hidden = true
            };
            var onOption = new Option<int[]>("--on")
            {
                Description = "Turn on the relay channels specified.",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            AddChannelCompletions(onOption);
            var legacyOnOption = CreateHiddenChannelOption("-on");
            var offOption = new Option<int[]>("--off")
            {
                Description = "Turn off the relay channels specified.",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            AddChannelCompletions(offOption);
            var legacyOffOption = CreateHiddenChannelOption("-off");
            var guiOption = CreateBoolOption("--gui", "Start the graphical user interface from a terminal.");
            var legacyGuiOption = CreateHiddenBoolOption("-gui");
            var completionCommand = CreateCompletionCommand();
            var rootCommand = new RootCommand("A simple utility to control, list, and query USB relay devices.");
            rootCommand.Options.Add(listOption);
            rootCommand.Options.Add(legacyListOption);
            rootCommand.Options.Add(statusOption);
            rootCommand.Options.Add(legacyStatusOption);
            rootCommand.Options.Add(serialOption);
            rootCommand.Options.Add(legacySerialOption);
            rootCommand.Options.Add(onOption);
            rootCommand.Options.Add(legacyOnOption);
            rootCommand.Options.Add(offOption);
            rootCommand.Options.Add(legacyOffOption);
            rootCommand.Options.Add(guiOption);
            rootCommand.Options.Add(legacyGuiOption);
            rootCommand.Subcommands.Add(completionCommand);
            rootCommand.SetAction(parseResult => 0);

            return new CliGrammar(
                rootCommand,
                listOption,
                legacyListOption,
                statusOption,
                legacyStatusOption,
                serialOption,
                legacySerialOption,
                onOption,
                legacyOnOption,
                offOption,
                legacyOffOption,
                guiOption,
                legacyGuiOption);
        }

        static Option<bool> CreateBoolOption(string name, string description)
        {
            var option = new Option<bool>(name)
            {
                Description = description
            };
            return option;
        }

        static Option<bool> CreateHiddenBoolOption(string name)
        {
            return new Option<bool>(name)
            {
                Hidden = true
            };
        }

        static Option<int[]> CreateHiddenChannelOption(string name)
        {
            var option = new Option<int[]>(name)
            {
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true,
                Hidden = true
            };
            AddChannelCompletions(option);
            return option;
        }

        static void AddChannelCompletions(Option<int[]> option)
        {
            option.CompletionSources.Add(ChannelCompletionValues);
        }

        static Command CreateCompletionCommand()
        {
            var command = new Command("complete", "Generate shell completion candidates.")
            {
                Hidden = true
            };
            command.Options.Add(new Option<string>("--line")
            {
                Description = "Full command line text.",
                Required = true
            });
            command.Options.Add(new Option<int>("--position")
            {
                Description = "Cursor position within the command line.",
                Required = true
            });
            command.SetAction(parseResult => 0);
            return command;
        }

        sealed class CliGrammar
        {
            public CliGrammar(
                RootCommand rootCommand,
                Option<bool> listOption,
                Option<bool> legacyListOption,
                Option<bool> statusOption,
                Option<bool> legacyStatusOption,
                Option<string> serialOption,
                Option<string> legacySerialOption,
                Option<int[]> onOption,
                Option<int[]> legacyOnOption,
                Option<int[]> offOption,
                Option<int[]> legacyOffOption,
                Option<bool> guiOption,
                Option<bool> legacyGuiOption)
            {
                RootCommand = rootCommand;
                ListOption = listOption;
                LegacyListOption = legacyListOption;
                StatusOption = statusOption;
                LegacyStatusOption = legacyStatusOption;
                SerialOption = serialOption;
                LegacySerialOption = legacySerialOption;
                OnOption = onOption;
                LegacyOnOption = legacyOnOption;
                OffOption = offOption;
                LegacyOffOption = legacyOffOption;
                GuiOption = guiOption;
                LegacyGuiOption = legacyGuiOption;
            }

            public RootCommand RootCommand { get; private set; }
            public Option<bool> ListOption { get; private set; }
            public Option<bool> LegacyListOption { get; private set; }
            public Option<bool> StatusOption { get; private set; }
            public Option<bool> LegacyStatusOption { get; private set; }
            public Option<string> SerialOption { get; private set; }
            public Option<string> LegacySerialOption { get; private set; }
            public Option<int[]> OnOption { get; private set; }
            public Option<int[]> LegacyOnOption { get; private set; }
            public Option<int[]> OffOption { get; private set; }
            public Option<int[]> LegacyOffOption { get; private set; }
            public Option<bool> GuiOption { get; private set; }
            public Option<bool> LegacyGuiOption { get; private set; }
        }

        sealed class ParsedCliCommand
        {
            public ParsedCliCommand(
                Operations operation,
                string serial,
                IEnumerable<int> onChannels,
                IEnumerable<int> offChannels,
                bool isGui,
                bool isHelpRequested,
                bool isVersionRequested,
                bool hasSystemCommandLineErrors,
                IEnumerable<string> errors)
            {
                Operation = operation;
                Serial = serial;
                OnChannels = onChannels.ToArray();
                OffChannels = offChannels.ToArray();
                IsGui = isGui;
                IsHelpRequested = isHelpRequested;
                IsVersionRequested = isVersionRequested;
                HasSystemCommandLineErrors = hasSystemCommandLineErrors;
                Errors = errors.ToArray();
            }

            public Operations Operation { get; private set; }
            public string Serial { get; private set; }
            public IEnumerable<int> OnChannels { get; private set; }
            public IEnumerable<int> OffChannels { get; private set; }
            public bool IsGui { get; private set; }
            public bool IsHelpRequested { get; private set; }
            public bool IsVersionRequested { get; private set; }
            public bool HasSystemCommandLineErrors { get; private set; }
            public IEnumerable<string> Errors { get; private set; }
            public bool IsValid { get { return !Errors.Any(); } }
            public bool HasRelayOptions
            {
                get
                {
                    return Operation != Operations.NULL
                        || !string.IsNullOrEmpty(Serial)
                        || OnChannels.Any()
                        || OffChannels.Any();
                }
            }
        }
    }
}

