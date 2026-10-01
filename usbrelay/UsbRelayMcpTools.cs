using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using usbrelay.Sequences;

namespace usbrelay
{
    public sealed class UsbRelayMcpTools : IDisposable
    {
        // A server serializes operations so a sequence cannot race another tool's
        // switch or repository update. The SDK remains free to handle ping/cancel.
        private readonly SemaphoreSlim operationLock = new SemaphoreSlim(1, 1);
        private readonly RelayService relay;
        private readonly SequenceRepository sequences;
        private readonly IRelayBackend backend;
        private readonly TextWriter log;
        private readonly string themePath;

        public UsbRelayMcpTools(RelayService relay, SequenceRepository sequences, IRelayBackend backend, TextWriter log, string themePath = null)
        {
            this.relay = relay;
            this.sequences = sequences;
            this.backend = backend;
            this.log = TextWriter.Synchronized(log);
            this.themePath = themePath ?? ThemeSettings.DefaultPath;
        }

        [McpServerTool(Name = "relay_list", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Refresh and list connected USB relay devices, full unique paths, friendly names and every channel's current ON/OFF state.")]
        public Task<CallToolResult> ListDevices(CancellationToken cancellationToken = default(CancellationToken))
            => Execute("relay_list", () => Result(new { devices = relay.EnumerateDevices().Select(DeviceInfo).ToArray() }), cancellationToken);

        [McpServerTool(Name = "relay_set_channel", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
        [Description("Set a physical relay channel ON or OFF. Select the device by unique full path, friendly name or unambiguous serial.")]
        public Task<CallToolResult> SetChannel(
            [Description("Device path, friendly device name or unique serial from relay_list.")] string device,
            [Description("Channel number as text, e.g. 1, or configured channel name.")] string channel,
            [Description("true turns the channel ON; false turns it OFF.")] bool on,
            CancellationToken cancellationToken = default(CancellationToken))
            => Execute("relay_set_channel", () =>
            {
                RelayDevice target = relay.GetDevice(Required(device, "device"));
                int number = target.ResolveChannel(Required(channel, "channel"));
                log.WriteLine("[MCP] Switching path=" + target.DevicePath + ", channel=" + number + ", on=" + on);
                relay.SetChannel(target, number, on);
                return Result(new { device = target.DevicePath, channel = number, on });
            }, cancellationToken);

        [McpServerTool(Name = "relay_all_off", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
        [Description("Turn OFF every channel on every connected USB relay board. Attempts remaining channels after a hardware failure and reports all errors.")]
        public Task<CallToolResult> AllOff(CancellationToken cancellationToken = default(CancellationToken))
            => Execute("relay_all_off", () =>
            {
                var errors = relay.AllOff(log.WriteLine);
                return Result(new { success = errors.Count == 0, errors }, errors.Count != 0);
            }, cancellationToken);

        [McpServerTool(Name = "relay_update_names", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Persist device/channel friendly names used by GUI, CLI and MCP. Omitted fields are preserved; empty names clear a saved name.")]
        public Task<CallToolResult> UpdateNames(
            [Description("Device path, friendly device name or unique serial.")] string device,
            [Description("New device name; empty clears it, omitted preserves it.")] string name = null,
            [Description("Map channel numbers (1..8) to friendly names; an empty value clears that channel's name.")] Dictionary<string, string> channels = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => Execute("relay_update_names", () =>
            {
                if (name == null && (channels == null || channels.Count == 0)) throw new ArgumentException("Supply name or channels.");
                var pairs = new List<string>();
                foreach (var pair in channels ?? new Dictionary<string, string>())
                {
                    if (pair.Value == null) throw new ArgumentException("Channel names must be strings; use an empty string to clear.");
                    pairs.Add(pair.Key);
                    pairs.Add(pair.Value);
                }
                relay.UpdateNames(Required(device, "device"), name, pairs);
                return Result(new { success = true });
            }, cancellationToken);

        [McpServerTool(Name = "sequence_list", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("List saved GUI sequences with name, description, run button, syntax validity and diagnostics. Does not run scripts or touch hardware.")]
        public Task<CallToolResult> ListSequences(CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_list", () => Result(new
            {
                sequences = sequences.Load().Select(sequence =>
                {
                    var parsed = SequenceParser.Parse(sequence.Script);
                    return new { name = sequence.Name, run_button = sequence.DisplayRunButtonText, description = sequence.Description, valid = parsed.IsValid, diagnostics = parsed.Diagnostics };
                }).ToArray()
            }), cancellationToken);

        [McpServerTool(Name = "sequence_read", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Read one saved sequence, including its complete script and editable metadata. Names are matched case-insensitively.")]
        public Task<CallToolResult> ReadSequence(string name, CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_read", () =>
            {
                var matches = sequences.Load().Where(sequence => string.Equals(sequence.Name, Required(name, "name"), StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length != 1) throw new ArgumentException(matches.Length == 0 ? "Sequence not found: " + name : "Duplicate sequence name: " + name);
                return Result(new { name = matches[0].Name, run_button = matches[0].DisplayRunButtonText, description = matches[0].Description, script = matches[0].Script });
            }, cancellationToken);

        [McpServerTool(Name = "sequence_add", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
        [Description("Save a new sequence in the GUI's repository. Does not execute it. Use sequence_functions for the supported script language.")]
        public Task<CallToolResult> AddSequence(string name, string script, string run_button = "Run", string description = "", CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_add", () => SequenceCommand(cli => cli.Add(name, Required(script, "script"), null, run_button, description), cancellationToken), cancellationToken);

        [McpServerTool(Name = "sequence_modify", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
        [Description("Update an existing saved sequence. Omitted fields are preserved. Does not execute the script.")]
        public Task<CallToolResult> ModifySequence(string name, string new_name = null, string script = null, string run_button = null, string description = null, CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_modify", () => SequenceCommand(cli => cli.Modify(name, new_name, script, null, run_button, description), cancellationToken), cancellationToken);

        [McpServerTool(Name = "sequence_remove", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
        [Description("Delete one saved sequence by name from the GUI's repository. This does not change relay states.")]
        public Task<CallToolResult> RemoveSequence(string name, CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_remove", () => SequenceCommand(cli => cli.Remove(name), cancellationToken), cancellationToken);

        [McpServerTool(Name = "sequence_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Validate saved sequence syntax and availability of referenced relay devices/channels. Omit name to check all sequences. Does not execute scripts.")]
        public Task<CallToolResult> SequenceStatus(string name = null, CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_status", () => SequenceCommand(cli => cli.Status(name), cancellationToken), cancellationToken);

        [McpServerTool(Name = "sequence_run", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
        [Description("Execute a saved sequence with real relay changes, delays and any saved external programs. Returns execution logs and failure details. Cancellation stops subsequent actions without undoing completed relay changes.")]
        public Task<CallToolResult> RunSequence(
            string name,
            [Description("Value returned by each sequence.Confirm call. true auto-accepts like the CLI; false follows the script's Cancel branch.")] bool confirm = true,
            CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_run", () => SequenceCommand(cli => cli.Run(name, false, cancellationToken,
                (title, message) => { log.WriteLine("[MCP] Confirmation title=" + title + ", result=" + confirm); return confirm; }), cancellationToken), cancellationToken);

        [McpServerTool(Name = "sequence_functions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Get reference documentation for all sequence functions, selectors, confirmations and control-flow operators.")]
        public Task<CallToolResult> SequenceFunctions(CancellationToken cancellationToken = default(CancellationToken))
            => Execute("sequence_functions", () => SequenceCommand(cli => cli.Functions(), cancellationToken), cancellationToken);

        [McpServerTool(Name = "gui_theme", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Query or persist the GUI theme (dark/light). Omit theme to read. New GUI windows use the saved preference.")]
        public Task<CallToolResult> Theme(string theme = null, CancellationToken cancellationToken = default(CancellationToken))
            => Execute("gui_theme", () =>
            {
                if (theme != null) new ThemeSettings { Theme = theme }.Save(themePath);
                return Result(new { theme = ThemeSettings.Load(themePath).Theme });
            }, cancellationToken);

        private async Task<CallToolResult> Execute(string operation, Func<CallToolResult> action, CancellationToken cancellationToken)
        {
            log.WriteLine("[MCP] " + operation + " requested; waiting for operation lock.");
            await operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        log.WriteLine("[MCP] " + operation + " started.");
                        CallToolResult result = action();
                        log.WriteLine("[MCP] " + operation + " completed; isError=" + result.IsError);
                        return result;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        log.WriteLine("[MCP] " + operation + " failed: " + ex);
                        return Result(new { error = ex.Message }, true);
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                operationLock.Release();
                log.WriteLine("[MCP] " + operation + " released operation lock.");
            }
        }

        private CallToolResult SequenceCommand(Func<SequenceCli, int> command, CancellationToken cancellationToken)
        {
            using (var output = new StringWriter())
            using (var error = new StringWriter())
            {
                var cli = new SequenceCli(sequences, backend, new ProcessExternalToolRunner(cancellationToken), output, error);
                int exitCode = command(cli);
                return Result(new { success = exitCode == 0, output = output.ToString(), error = error.ToString() }, exitCode != 0);
            }
        }

        private static object DeviceInfo(RelayDevice device) => new
        {
            name = device.DisplayName,
            serial = device.SerialNumber,
            path = device.DevicePath,
            type = device.Type.ToString(),
            channels = Enumerable.Range(1, device.ChannelCount).Select(number => new { channel = number, name = device.GetChannelName(number), on = device.IsChannelOn(number) }).ToArray()
        };

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
            return value;
        }

        private static CallToolResult Result(object value, bool isError = false)
        {
            return new CallToolResult
            {
                IsError = isError,
                Content = new List<ContentBlock> { new TextContentBlock { Text = JsonSerializer.Serialize(value) } },
                StructuredContent = JsonSerializer.SerializeToElement(value)
            };
        }

        public void Dispose() { operationLock.Dispose(); }
    }
}
