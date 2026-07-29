# usbrelay

This small utility was initially implemented to automate power management for different components by controlling multiple USB-Relay boards on the windows OS based astrophotography control computer. 

The implementation is based on the great work of [usb-relay-hid](https://github.com/pavel-a/usb-relay-hid) project by [pavel-a](https://github.com/pavel-a). Most of the common USB-Relay boards from Amazon, Aliexpress, or Taobao are supported. This project provides a simple control utility for querying and controlling multiple USB-Relay boards connected to the Windows computer.

![2-Channel & 8-Channel USB-Relay Boards](https://github.com/mxcoppell/usbrelay/blob/master/images/usbrelay-boards.jpg?raw=true)

The binary provided is a stand-alone executable. All required dynamic link libraries are embedded into the the single executable. 

To build this project, please add [Costura.Fody](https://github.com/Fody/Costura) to the project. (Right click 'usbrelay' project in Visual Studio. Select 'Manage NuGet Packages...')

## Identifying and naming USB-Relay boards

Many boards use the same serial number, such as `BITFT` or `6QMBS`. The application enumerates every connected board and keeps its complete Windows HID device path. The path uniquely identifies a board even when serial numbers are duplicated.

Run `usbrelay --list` to see all boards. In the GUI, click **Edit names** to assign a friendly device name and optional names to its channels. Names are stored in `%APPDATA%\usbrelay\device-names.json` and are keyed by the unique device path, so adding more boards does not require changing the application.

For the two boards in the example, the unique path portions are `1746991` and `2a5582f3`. After assigning names such as `DUT power` and `Fan power`, use those names from the CLI:

```
usbrelay --list
usbrelay --device-name "DUT power" --on-name "Main relay"
usbrelay --device-name "Fan power" --off-name "Cooling fan"
usbrelay --device-path "<full path from --list>" --set-device-name "DUT power"
usbrelay --device-name "DUT power" --set-channel-name 1 "Main relay" 2 Debug
```

Numeric selectors remain available. The full device path can also be used when no friendly name has been configured:

```
usbrelay --device-path "<full path from --list>" --on 1
```

Device names must be unique. Channel names must be unique within each device. If a device has no configured channel name, the GUI and `--list` display `CH1`, `CH2`, and so on.

Names can also be changed from the CLI. Use `--serial` or `--device-path` when assigning the first device name; use the existing `--device-name` when renaming an already named device. Channel updates are supplied as repeated channel/name pairs:

```
usbrelay --serial 6QMBS --set-device-name "DUT power"
usbrelay --device-name "DUT power" --set-channel-name 1 "Main relay" 2 Debug
```

The easiest way to do this is to use a Linux box. For example, a Raspberry Pi. 

Install 'usbrelay' package.
```
> sudo apt install usbrelay
```

Change the default board serial number 'BITFT' to 'NEWBN'(just an example, your own serial number preferred).
```
> sudo usbrelay BITFT=NEWBN
BITFT_1=0
BITFT_2=0
Setting new serial
```

Verify the new serial number.
```
> sudo usbrelay
NEWBN_1=0
NEWBN_2=0
```

# binary release

[Version 1.0.0.5](https://github.com/dm17ryk/usbrelay/releases/tag/v1.0.0.5) - installer and portable zip are built by GitHub Actions.

[Version 1.0.0.3](https://github.com/mxcoppell/usbrelay/releases/tag/1.0.0.3) - [usbrelay-v1.0.0.3.zip](https://github.com/mxcoppell/usbrelay/releases/download/1.0.0.3/usbrelay-v1.0.0.3.zip)

## Creating a release

The release script increments the fourth version component by default, builds
the `Release` configuration, runs the tests, creates the portable ZIP, and
creates the NSIS installer in `artifacts\release`:

```powershell
.\scripts\Release.ps1
```

Other version increments are available:

```powershell
.\scripts\Release.ps1 -Increment Patch
.\scripts\Release.ps1 -Increment Minor
.\scripts\Release.ps1 -Increment Major
.\scripts\Release.ps1 -Version 1.1.0.0
```

Use `-WhatIf` to preview the version change without modifying files or
building. The script restores `Version.props` automatically if the build or
installer step fails. It does not create a Git commit or tag; create and push
the matching tag separately when the artifacts are verified.

# current state

Current version is implemented in C# (VS2019). Porting to other platforms should be straightfoward. 

# help

```
Description:
  A simple utility to control, list, and query USB-Relay devices.

Usage:
  usbrelay [options]
  usbrelay sequence <query|status|run> [--name <name>]

Options:
  --list             List all available serial numbers of connected USB-Relay devices.
  --status           Display relay channel status for connected USB-Relay devices.
  --serial <serial>  Specify the serial number of the USB-Relay device to operate.
  --device-path <path> Specify the unique device path shown by --list.
  --device-name <name> Specify the friendly device name configured in the GUI.
  --set-device-name <name> Set/update the friendly name for the selected device.
  --set-channel-name <pairs> Set channel names as `<channel> <name>` pairs.
  --on <on>          Turn on the relay channels specified.
  --on-name <name>   Turn on named relay channels specified.
  --off <off>        Turn off the relay channels specified.
  --off-name <name>  Turn off named relay channels specified.
  --gui              Start the graphical user interface from a terminal.
  -?, -h, --help     Show help and usage information
  --version          Show version information

Commands:
  sequence           Query, validate, and run saved GUI sequences.

Examples:
  usbrelay --list
  usbrelay --status
  usbrelay --serial BITFT --on 1
  usbrelay --device-name "DUT power" --on-name "Main relay"
  usbrelay --device-name "Fan power" --off-name "Cooling fan"
  usbrelay --device-path "<path from --list>" --set-device-name "DUT power"
  usbrelay --device-name "DUT power" --set-channel-name 1 "Main relay" 2 Debug
  usbrelay --serial BITFT --on 1 2 3
  usbrelay --serial BITFT --off 2
  usbrelay --serial BITFT --on 1 3 5 --off 2 4 6
  usbrelay --gui
  usbrelay sequence query
  usbrelay sequence status --name "Power cycle DUT"
  usbrelay sequence run --name "Power cycle DUT"
```

The legacy single-dash options are still accepted for compatibility: `-list`,
`-status`, `-serial`, `-on`, `-off`, and `-gui`. `-v` is accepted as a short
alias for `--version`.

# sequence CLI

Saved GUI sequences can be inspected, created, modified, and run from the
command line with the `sequence` command group. The CLI uses the same local
sequence repository as the GUI.

```
usbrelay sequence query
usbrelay sequence query --name "Power cycle DUT"
usbrelay sequence read --name "Power cycle DUT"
usbrelay sequence add --name "Power cycle DUT" --script-file .\power-cycle.sequence --run-button "Power cycle"
usbrelay sequence modify --name "Power cycle DUT" --new-name "Power cycle DUT v2" --script-file .\power-cycle-v2.sequence
usbrelay sequence status
usbrelay sequence status --name "Power cycle DUT"
usbrelay sequence run --name "Power cycle DUT"
usbrelay sequence functions
```

`query` prints saved sequence summaries, or one detailed sequence when `--name`
is provided. `read` prints one detailed sequence and requires `--name`. `add`
creates a sequence; provide either `--script "..."` or `--script-file <path>`.
`modify` updates any supplied fields (`--new-name`, `--script` or
`--script-file`, `--run-button`, and `--description`) and leaves omitted fields
unchanged. `status` validates saved scripts and checks their relay resources
against the currently connected USB-Relay devices. `run` requires `--name` and
executes the saved sequence with real delays and any saved external-tool steps.
`functions` prints every supported sequence function, control-flow operator, and
value. Sequence names are matched case-insensitively.

Sequence scripts accept either numeric selectors or configured friendly names.
Existing numeric scripts remain valid:

```
sequence.PowerOn("6QMBS", 1);
sequence.PowerOff("6QMBS", 2);
```

GUI-only confirmations return `true` for OK and `false` for Cancel. CLI runs
automatically return `true` without opening a dialog. Use `Exit` for an
intentional successful stop:

```
var result = sequence.Confirm("Confirm power cycle", "Start the power cycle?");
if (!result) {
    sequence.Exit("User cancelled");
}
```

`Fail` remains a failed sequence outcome; `Exit` logs its message, skips the
remaining actions, and reports a successful stopped run.

Named scripts are easier to read and identify the correct board when several
boards have the same serial number:

```
sequence.PowerOn("DUT power", "Main relay");
sequence.WaitChannel("DUT power", "Main relay", RelayState.On, 3000);
sequence.ReadChannel("Fan power", "Cooling fan");
```

# shell completion

PowerShell and pwsh can load the native argument completer directly from this
repository. For one session:

```
PS D:\bin> . D:\path\to\usbrelay\scripts\usbrelay-completion.ps1 -CommandPath D:\bin\usbrelay.exe
```

To load it for every PowerShell session, add the same dot-source command to
`$PROFILE`:

```
PS D:\bin> Add-Content $PROFILE '. D:\path\to\usbrelay\scripts\usbrelay-completion.ps1 -CommandPath D:\bin\usbrelay.exe'
```

The PowerShell completer registers both `usbrelay` and `usbrelay.exe`. It
completes options, help/version aliases, GUI mode, legacy single-dash aliases,
the `sequence` command group, sequence subcommands, `--name`, and channel values
`1` through `8` for `--on`, `-on`, `--off`, and `-off`.
The script also creates a session-local `usbrelay` alias to the `-CommandPath`
target. Use that alias for completion instead of registering relative paths such
as `.\usbrelay\bin\Debug\usbrelay.exe`; some PowerShell completion/menu modules
build regular expressions from command names and do not escape backslashes.
When loaded, the script also removes stale usbrelay path aliases from
`PSCompletions` state if that module is present.

Clink provides programmable completion for `cmd.exe`. Copy or link the Clink
script into one of Clink's loaded script directories, then restart `cmd.exe` or
reload Clink scripts:

```
copy D:\path\to\usbrelay\scripts\clink\completions\usbrelay.lua %LOCALAPPDATA%\clink\
```

If `usbrelay.exe` is not on `PATH`, set `USBRELAY_COMPLETION_COMMAND` to the
full executable path before starting `cmd.exe`:

```
setx USBRELAY_COMPLETION_COMMAND D:\bin\usbrelay.exe
```

Stock `cmd.exe` does not expose a programmable completion API; `cmd` completion
support is provided through Clink.

# examples

List all availabe USB-Relay boards.
```
PS D:\bin>
PS D:\bin> .\usbrelay.exe -list
Serial    Type
------    ----
SMLFT     TwoChannel
BIGFT     TwoChannel
```

Display the status of all availabe ports.
```
PS D:\bin>
PS D:\bin> .\usbrelay.exe -status
Serial   C1   C2   C3   C4   C5   C6   C7   C8
------  ---- ---- ---- ---- ---- ---- ---- ----
SMLFT   OFF  OFF
BIGFT   OFF  OFF
```

On board with serial BIGFT, turn on channel 1 and turn off channel 2.
```
PS D:\bin>
PS D:\bin> .\usbrelay.exe -serial BIGFT -on 1 -off 2
Turn on channel 1 on BIGFT: success
Turn off channel 2 on BIGFT: success

Serial   C1   C2   C3   C4   C5   C6   C7   C8
------  ---- ---- ---- ---- ---- ---- ---- ----
SMLFT   OFF  OFF
BIGFT   ON   OFF
```

On board with serial SMLFT, turn on both channel 1 & 2.
```
PS D:\bin>
PS D:\bin> .\usbrelay.exe -serial SMLFT -on 1 2
Turn on channel 1 on SMLFT: success
Turn on channel 2 on SMLFT: success

Serial   C1   C2   C3   C4   C5   C6   C7   C8
------  ---- ---- ---- ---- ---- ---- ---- ----
SMLFT   ON   ON
BIGFT   ON   OFF
```

On board with serial BIGFT, turn off both channel 1 & 2.
```
PS D:\bin>
PS D:\bin> .\usbrelay.exe -serial BIGFT -off 1 2
Turn off channel 1 on BIGFT: success
Turn off channel 2 on BIGFT: success

Serial   C1   C2   C3   C4   C5   C6   C7   C8
------  ---- ---- ---- ---- ---- ---- ---- ----
SMLFT   ON   ON
BIGFT   OFF  OFF
```

# license

The shared library [usb_relay_device.dll] and helper code [UsbRelayDeviceHelper.cs](https://github.com/mxcoppell/usbrelay/blob/master/usbrelay/UsbRelayDeviceHelper.cs) are from project [usb-relay-hid](https://github.com/pavel-a/usb-relay-hid). They are dual-licensed: GPL + commercial.

There are no software packages (SDK) provided by the hardware vendors.
