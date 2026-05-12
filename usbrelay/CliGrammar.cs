using System;
using System.CommandLine;
using System.CommandLine.Completions;
using System.Linq;

namespace usbrelay
{
    internal sealed class CliGrammar
    {
        private static readonly Lazy<CliGrammar> Cached = new Lazy<CliGrammar>(Create);
        private static readonly string[] ChannelCompletionValues = Enumerable.Range(1, 8).Select(channel => channel.ToString()).ToArray();

        private CliGrammar(
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

        public static CliGrammar Current { get { return Cached.Value; } }

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

        private static CliGrammar Create()
        {
            var listOption = CreateBoolOption("--list", "List all available serial numbers of connected USB-Relay devices.");
            var legacyListOption = CreateHiddenBoolOption("-list");
            var statusOption = CreateBoolOption("--status", "Display relay channel status for connected USB-Relay devices.");
            var legacyStatusOption = CreateHiddenBoolOption("-status");
            var serialOption = new Option<string>("--serial")
            {
                Description = "Specify the serial number of the USB-Relay device to operate."
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
            var legacyVersionOption = CreateHiddenBoolOption("-v");
            var completionCommand = CreateCompletionCommand();
            var rootCommand = new RootCommand("A simple utility to control, list, and query USB-Relay devices.");
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
            rootCommand.Options.Add(legacyVersionOption);
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

        private static Option<bool> CreateBoolOption(string name, string description)
        {
            return new Option<bool>(name)
            {
                Description = description
            };
        }

        private static Option<bool> CreateHiddenBoolOption(string name)
        {
            return new Option<bool>(name)
            {
                Hidden = true
            };
        }

        private static Option<int[]> CreateHiddenChannelOption(string name)
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

        private static void AddChannelCompletions(Option<int[]> option)
        {
            option.CompletionSources.Add(ChannelCompletionValues);
        }

        private static Command CreateCompletionCommand()
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
    }
}
