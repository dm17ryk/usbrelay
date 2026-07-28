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
            Option<string> devicePathOption,
            Option<string> deviceNameOption,
            Option<string> setDeviceNameOption,
            Option<string[]> setChannelNameOption,
            Option<int[]> onOption,
            Option<int[]> legacyOnOption,
            Option<string[]> onNameOption,
            Option<int[]> offOption,
            Option<int[]> legacyOffOption,
            Option<string[]> offNameOption,
            Option<bool> guiOption,
            Option<bool> legacyGuiOption,
            Command sequenceCommand,
            Command sequenceQueryCommand,
            Command sequenceStatusCommand,
            Command sequenceRunCommand,
            Option<string> sequenceQueryNameOption,
            Option<string> sequenceStatusNameOption,
            Option<string> sequenceRunNameOption,
            Command sequenceAddCommand,
            Command sequenceReadCommand,
            Command sequenceModifyCommand,
            Command sequenceFunctionsCommand,
            Option<string> sequenceAddNameOption,
            Option<string> sequenceAddScriptOption,
            Option<string> sequenceAddScriptFileOption,
            Option<string> sequenceAddRunButtonOption,
            Option<string> sequenceAddDescriptionOption,
            Option<string> sequenceReadNameOption,
            Option<string> sequenceModifyNameOption,
            Option<string> sequenceModifyNewNameOption,
            Option<string> sequenceModifyScriptOption,
            Option<string> sequenceModifyScriptFileOption,
            Option<string> sequenceModifyRunButtonOption,
            Option<string> sequenceModifyDescriptionOption)
        {
            RootCommand = rootCommand;
            ListOption = listOption;
            LegacyListOption = legacyListOption;
            StatusOption = statusOption;
            LegacyStatusOption = legacyStatusOption;
            SerialOption = serialOption;
            LegacySerialOption = legacySerialOption;
            DevicePathOption = devicePathOption;
            DeviceNameOption = deviceNameOption;
            SetDeviceNameOption = setDeviceNameOption;
            SetChannelNameOption = setChannelNameOption;
            OnOption = onOption;
            LegacyOnOption = legacyOnOption;
            OnNameOption = onNameOption;
            OffOption = offOption;
            LegacyOffOption = legacyOffOption;
            OffNameOption = offNameOption;
            GuiOption = guiOption;
            LegacyGuiOption = legacyGuiOption;
            SequenceCommand = sequenceCommand;
            SequenceQueryCommand = sequenceQueryCommand;
            SequenceStatusCommand = sequenceStatusCommand;
            SequenceRunCommand = sequenceRunCommand;
            SequenceQueryNameOption = sequenceQueryNameOption;
            SequenceStatusNameOption = sequenceStatusNameOption;
            SequenceRunNameOption = sequenceRunNameOption;
            SequenceAddCommand = sequenceAddCommand;
            SequenceReadCommand = sequenceReadCommand;
            SequenceModifyCommand = sequenceModifyCommand;
            SequenceFunctionsCommand = sequenceFunctionsCommand;
            SequenceAddNameOption = sequenceAddNameOption;
            SequenceAddScriptOption = sequenceAddScriptOption;
            SequenceAddScriptFileOption = sequenceAddScriptFileOption;
            SequenceAddRunButtonOption = sequenceAddRunButtonOption;
            SequenceAddDescriptionOption = sequenceAddDescriptionOption;
            SequenceReadNameOption = sequenceReadNameOption;
            SequenceModifyNameOption = sequenceModifyNameOption;
            SequenceModifyNewNameOption = sequenceModifyNewNameOption;
            SequenceModifyScriptOption = sequenceModifyScriptOption;
            SequenceModifyScriptFileOption = sequenceModifyScriptFileOption;
            SequenceModifyRunButtonOption = sequenceModifyRunButtonOption;
            SequenceModifyDescriptionOption = sequenceModifyDescriptionOption;
        }

        public static CliGrammar Current { get { return Cached.Value; } }

        public RootCommand RootCommand { get; private set; }
        public Option<bool> ListOption { get; private set; }
        public Option<bool> LegacyListOption { get; private set; }
        public Option<bool> StatusOption { get; private set; }
        public Option<bool> LegacyStatusOption { get; private set; }
        public Option<string> SerialOption { get; private set; }
        public Option<string> LegacySerialOption { get; private set; }
        public Option<string> DevicePathOption { get; private set; }
        public Option<string> DeviceNameOption { get; private set; }
        public Option<string> SetDeviceNameOption { get; private set; }
        public Option<string[]> SetChannelNameOption { get; private set; }
        public Option<int[]> OnOption { get; private set; }
        public Option<int[]> LegacyOnOption { get; private set; }
        public Option<string[]> OnNameOption { get; private set; }
        public Option<int[]> OffOption { get; private set; }
        public Option<int[]> LegacyOffOption { get; private set; }
        public Option<string[]> OffNameOption { get; private set; }
        public Option<bool> GuiOption { get; private set; }
        public Option<bool> LegacyGuiOption { get; private set; }
        public Command SequenceCommand { get; private set; }
        public Command SequenceQueryCommand { get; private set; }
        public Command SequenceStatusCommand { get; private set; }
        public Command SequenceRunCommand { get; private set; }
        public Option<string> SequenceQueryNameOption { get; private set; }
        public Option<string> SequenceStatusNameOption { get; private set; }
        public Option<string> SequenceRunNameOption { get; private set; }
        public Command SequenceAddCommand { get; private set; }
        public Command SequenceReadCommand { get; private set; }
        public Command SequenceModifyCommand { get; private set; }
        public Command SequenceFunctionsCommand { get; private set; }
        public Option<string> SequenceAddNameOption { get; private set; }
        public Option<string> SequenceAddScriptOption { get; private set; }
        public Option<string> SequenceAddScriptFileOption { get; private set; }
        public Option<string> SequenceAddRunButtonOption { get; private set; }
        public Option<string> SequenceAddDescriptionOption { get; private set; }
        public Option<string> SequenceReadNameOption { get; private set; }
        public Option<string> SequenceModifyNameOption { get; private set; }
        public Option<string> SequenceModifyNewNameOption { get; private set; }
        public Option<string> SequenceModifyScriptOption { get; private set; }
        public Option<string> SequenceModifyScriptFileOption { get; private set; }
        public Option<string> SequenceModifyRunButtonOption { get; private set; }
        public Option<string> SequenceModifyDescriptionOption { get; private set; }

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
            var devicePathOption = new Option<string>("--device-path")
            {
                Description = "Specify the unique USB device path shown by --list."
            };
            var deviceNameOption = new Option<string>("--device-name")
            {
                Description = "Specify the friendly device name configured in the GUI."
            };
            var setDeviceNameOption = new Option<string>("--set-device-name")
            {
                Description = "Set or update the friendly device name for the selected device."
            };
            var setChannelNameOption = new Option<string[]>("--set-channel-name")
            {
                Description = "Set channel names using pairs: <channel> <name> ...",
                Arity = ArgumentArity.OneOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            var onOption = new Option<int[]>("--on")
            {
                Description = "Turn on the relay channels specified.",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            AddChannelCompletions(onOption);
            var legacyOnOption = CreateHiddenChannelOption("-on");
            var onNameOption = CreateNameOption("--on-name", "Turn on the named relay channels specified.");
            var offOption = new Option<int[]>("--off")
            {
                Description = "Turn off the relay channels specified.",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            AddChannelCompletions(offOption);
            var legacyOffOption = CreateHiddenChannelOption("-off");
            var offNameOption = CreateNameOption("--off-name", "Turn off the named relay channels specified.");
            var guiOption = CreateBoolOption("--gui", "Start the graphical user interface from a terminal.");
            var legacyGuiOption = CreateHiddenBoolOption("-gui");
            var legacyVersionOption = CreateHiddenBoolOption("-v");
            var completionCommand = CreateCompletionCommand();
            Command sequenceQueryCommand;
            Command sequenceStatusCommand;
            Command sequenceRunCommand;
            Command sequenceAddCommand;
            Command sequenceReadCommand;
            Command sequenceModifyCommand;
            Command sequenceFunctionsCommand;
            Option<string> sequenceQueryNameOption;
            Option<string> sequenceStatusNameOption;
            Option<string> sequenceRunNameOption;
            Option<string> sequenceAddNameOption;
            Option<string> sequenceAddScriptOption;
            Option<string> sequenceAddScriptFileOption;
            Option<string> sequenceAddRunButtonOption;
            Option<string> sequenceAddDescriptionOption;
            Option<string> sequenceReadNameOption;
            Option<string> sequenceModifyNameOption;
            Option<string> sequenceModifyNewNameOption;
            Option<string> sequenceModifyScriptOption;
            Option<string> sequenceModifyScriptFileOption;
            Option<string> sequenceModifyRunButtonOption;
            Option<string> sequenceModifyDescriptionOption;
            var sequenceCommand = CreateSequenceCommand(
                out sequenceQueryCommand,
                out sequenceStatusCommand,
                out sequenceRunCommand,
                out sequenceAddCommand,
                out sequenceReadCommand,
                out sequenceModifyCommand,
                out sequenceFunctionsCommand,
                out sequenceQueryNameOption,
                out sequenceStatusNameOption,
                out sequenceRunNameOption,
                out sequenceAddNameOption,
                out sequenceAddScriptOption,
                out sequenceAddScriptFileOption,
                out sequenceAddRunButtonOption,
                out sequenceAddDescriptionOption,
                out sequenceReadNameOption,
                out sequenceModifyNameOption,
                out sequenceModifyNewNameOption,
                out sequenceModifyScriptOption,
                out sequenceModifyScriptFileOption,
                out sequenceModifyRunButtonOption,
                out sequenceModifyDescriptionOption);
            var rootCommand = new RootCommand("A simple utility to control, list, and query USB-Relay devices.");
            rootCommand.Options.Add(listOption);
            rootCommand.Options.Add(legacyListOption);
            rootCommand.Options.Add(statusOption);
            rootCommand.Options.Add(legacyStatusOption);
            rootCommand.Options.Add(serialOption);
            rootCommand.Options.Add(legacySerialOption);
            rootCommand.Options.Add(devicePathOption);
            rootCommand.Options.Add(deviceNameOption);
            rootCommand.Options.Add(setDeviceNameOption);
            rootCommand.Options.Add(setChannelNameOption);
            rootCommand.Options.Add(onOption);
            rootCommand.Options.Add(legacyOnOption);
            rootCommand.Options.Add(onNameOption);
            rootCommand.Options.Add(offOption);
            rootCommand.Options.Add(legacyOffOption);
            rootCommand.Options.Add(offNameOption);
            rootCommand.Options.Add(guiOption);
            rootCommand.Options.Add(legacyGuiOption);
            rootCommand.Options.Add(legacyVersionOption);
            rootCommand.Subcommands.Add(sequenceCommand);
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
                devicePathOption,
                deviceNameOption,
                setDeviceNameOption,
                setChannelNameOption,
                onOption,
                legacyOnOption,
                onNameOption,
                offOption,
                legacyOffOption,
                offNameOption,
                guiOption,
                legacyGuiOption,
                sequenceCommand,
                sequenceQueryCommand,
                sequenceStatusCommand,
                sequenceRunCommand,
                sequenceQueryNameOption,
                sequenceStatusNameOption,
                sequenceRunNameOption,
                sequenceAddCommand,
                sequenceReadCommand,
                sequenceModifyCommand,
                sequenceFunctionsCommand,
                sequenceAddNameOption,
                sequenceAddScriptOption,
                sequenceAddScriptFileOption,
                sequenceAddRunButtonOption,
                sequenceAddDescriptionOption,
                sequenceReadNameOption,
                sequenceModifyNameOption,
                sequenceModifyNewNameOption,
                sequenceModifyScriptOption,
                sequenceModifyScriptFileOption,
                sequenceModifyRunButtonOption,
                sequenceModifyDescriptionOption);
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

        private static Option<string[]> CreateNameOption(string name, string description)
        {
            return new Option<string[]>(name)
            {
                Description = description,
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };
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

        private static Command CreateSequenceCommand(
            out Command queryCommand,
            out Command statusCommand,
            out Command runCommand,
            out Command addCommand,
            out Command readCommand,
            out Command modifyCommand,
            out Command functionsCommand,
            out Option<string> queryNameOption,
            out Option<string> statusNameOption,
            out Option<string> runNameOption,
            out Option<string> addNameOption,
            out Option<string> addScriptOption,
            out Option<string> addScriptFileOption,
            out Option<string> addRunButtonOption,
            out Option<string> addDescriptionOption,
            out Option<string> readNameOption,
            out Option<string> modifyNameOption,
            out Option<string> modifyNewNameOption,
            out Option<string> modifyScriptOption,
            out Option<string> modifyScriptFileOption,
            out Option<string> modifyRunButtonOption,
            out Option<string> modifyDescriptionOption)
        {
            var command = new Command("sequence", "Query, validate, and run saved GUI sequences.");

            queryNameOption = new Option<string>("--name")
            {
                Description = "Saved sequence name."
            };
            queryCommand = new Command("query", "List saved sequences or show one sequence.")
            {
                Options = { queryNameOption }
            };
            queryCommand.SetAction(parseResult => 0);

            statusNameOption = new Option<string>("--name")
            {
                Description = "Saved sequence name."
            };
            statusCommand = new Command("status", "Validate saved sequence readiness.")
            {
                Options = { statusNameOption }
            };
            statusCommand.SetAction(parseResult => 0);

            runNameOption = new Option<string>("--name")
            {
                Description = "Saved sequence name.",
                Required = true
            };
            runCommand = new Command("run", "Run a saved sequence.")
            {
                Options = { runNameOption }
            };
            runCommand.SetAction(parseResult => 0);

            addNameOption = new Option<string>("--name")
            {
                Description = "New saved sequence name.",
                Required = true
            };
            addScriptOption = new Option<string>("--script")
            {
                Description = "Inline sequence script. Use --script-file for a file-based script."
            };
            addScriptFileOption = new Option<string>("--script-file")
            {
                Description = "Path to a text file containing the sequence script."
            };
            addRunButtonOption = new Option<string>("--run-button")
            {
                Description = "Run button text."
            };
            addDescriptionOption = new Option<string>("--description")
            {
                Description = "Sequence description."
            };
            addCommand = new Command("add", "Add a saved sequence.")
            {
                Options =
                {
                    addNameOption,
                    addScriptOption,
                    addScriptFileOption,
                    addRunButtonOption,
                    addDescriptionOption
                }
            };
            addCommand.SetAction(parseResult => 0);

            readNameOption = new Option<string>("--name")
            {
                Description = "Saved sequence name.",
                Required = true
            };
            readCommand = new Command("read", "Read one saved sequence in detail.")
            {
                Options = { readNameOption }
            };
            readCommand.SetAction(parseResult => 0);

            modifyNameOption = new Option<string>("--name")
            {
                Description = "Existing saved sequence name.",
                Required = true
            };
            modifyNewNameOption = new Option<string>("--new-name")
            {
                Description = "New sequence name."
            };
            modifyScriptOption = new Option<string>("--script")
            {
                Description = "Replacement inline sequence script."
            };
            modifyScriptFileOption = new Option<string>("--script-file")
            {
                Description = "Path to a replacement sequence script file."
            };
            modifyRunButtonOption = new Option<string>("--run-button")
            {
                Description = "Replacement run button text."
            };
            modifyDescriptionOption = new Option<string>("--description")
            {
                Description = "Replacement sequence description."
            };
            modifyCommand = new Command("modify", "Modify an existing saved sequence.")
            {
                Options =
                {
                    modifyNameOption,
                    modifyNewNameOption,
                    modifyScriptOption,
                    modifyScriptFileOption,
                    modifyRunButtonOption,
                    modifyDescriptionOption
                }
            };
            modifyCommand.SetAction(parseResult => 0);

            functionsCommand = new Command("functions", "Print sequence functions, operators, and values.");
            functionsCommand.SetAction(parseResult => 0);

            command.Subcommands.Add(queryCommand);
            command.Subcommands.Add(statusCommand);
            command.Subcommands.Add(runCommand);
            command.Subcommands.Add(addCommand);
            command.Subcommands.Add(readCommand);
            command.Subcommands.Add(modifyCommand);
            command.Subcommands.Add(functionsCommand);
            command.SetAction(parseResult => 0);
            return command;
        }
    }
}
