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

namespace usbrelay
{
    class UsbRelayWrapper
    {
        private string target_serial { get; set; }
        private string target_device_path { get; set; }
        private string target_device_name { get; set; }
        private readonly RelayService relayService;

        public UsbRelayWrapper(string serial)
            : this(serial, string.Empty, string.Empty, new RelayService(new NativeUsbRelayBackend()))
        {
        }

        public UsbRelayWrapper(string serial, string devicePath)
            : this(serial, devicePath, string.Empty, new RelayService(new NativeUsbRelayBackend()))
        {
        }

        public UsbRelayWrapper(string serial, string devicePath, string deviceName)
            : this(serial, devicePath, deviceName, new RelayService(new NativeUsbRelayBackend()))
        {
        }

        public UsbRelayWrapper(string serial, RelayService relayService)
            : this(serial, string.Empty, string.Empty, relayService)
        {
        }

        public UsbRelayWrapper(string serial, string devicePath, RelayService relayService)
            : this(serial, devicePath, string.Empty, relayService)
        {
        }

        public UsbRelayWrapper(string serial, string devicePath, string deviceName, RelayService relayService)
        {
            target_serial = serial;
            target_device_path = devicePath;
            target_device_name = deviceName;
            this.relayService = relayService;
        }
        ~UsbRelayWrapper() { }

        public IntPtr open_device_handle(string serial)
        {
            if (serial != "")
            {
                IntPtr retval = UsbRelayDeviceHelper.OpenWithSerialNumber(serial, serial.Length);
                return retval;
            }
            return IntPtr.Zero;
        }

        public int on_off_channels(HashSet<int> on_channels, HashSet<int> off_channels)
        {
            return on_off_channels(on_channels, off_channels, new HashSet<string>(), new HashSet<string>());
        }

        public int on_off_channels(
            HashSet<int> on_channels,
            HashSet<int> off_channels,
            HashSet<string> on_channel_names,
            HashSet<string> off_channel_names)
        {
            bool success = true;
            foreach (int i in on_channels)
            {
                string result = run_channel_operation(i, true);
                success = success && result == "success";
                Console.WriteLine(String.Format("Turn on channel {0} on {1}: {2}", i, TargetDescription(), result));
            }
            foreach (int i in off_channels)
            {
                string result = run_channel_operation(i, false);
                success = success && result == "success";
                Console.WriteLine(String.Format("Turn off channel {0} on {1}: {2}", i, TargetDescription(), result));
            }
            foreach (string name in on_channel_names)
            {
                string result = run_channel_operation(name, true);
                success = success && result == "success";
                Console.WriteLine(String.Format("Turn on channel {0} on {1}: {2}", name, TargetDescription(), result));
            }
            foreach (string name in off_channel_names)
            {
                string result = run_channel_operation(name, false);
                success = success && result == "success";
                Console.WriteLine(String.Format("Turn off channel {0} on {1}: {2}", name, TargetDescription(), result));
            }

            Console.WriteLine();
            status();
            return success ? 0 : 1;
        }

        public int update_names(string deviceName, string[] channelNamePairs)
        {
            try
            {
                RelayDevice target = ResolveTargetDevice();
                relayService.UpdateNames(target, deviceName, channelNamePairs);
                Console.WriteLine("Updated names for " + TargetDescription());
                status();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private string run_channel_operation(int channel, bool on)
        {
            return run_channel_operation(channel.ToString(), on);
        }

        private string run_channel_operation(string channelSelector, bool on)
        {
            try
            {
                RelayDevice target = ResolveTargetDevice();
                int channel = target.ResolveChannel(channelSelector);
                if (on)
                    relayService.TurnOn(target, channel);
                else
                    relayService.TurnOff(target, channel);
                return "success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public void list()
        {
            var devices = relayService.EnumerateDevices();
            if (devices.Count > 0)
            {
                Console.WriteLine("Name              Serial    Type              Device Path");
                Console.WriteLine("----              ------    ----              -----------");
            }

            foreach (var device in devices)
            {
                string channels = string.Join(", ", Enumerable.Range(1, device.ChannelCount)
                    .Select(channel => device.GetChannelName(channel) + "=" + channel));
                Console.WriteLine(String.Format("{0,-17} {1,-9} {2,-17} {3} [{4}]", device.DisplayName, device.SerialNumber, device.Type, device.DevicePath, channels));
            }
        }

        public void status()
        {
            var devices = relayService.EnumerateDevices();
            var rows = devices.Select(device => new StatusRow(device)).ToArray();
            if (rows.Length == 0)
                return;

            int channelCount = rows.Max(row => row.Channels.Length);
            var headers = new List<string> { "Name", "Serial", "Device Path" };
            for (int channel = 1; channel <= channelCount; channel++)
                headers.Add("CH" + channel);

            var widths = headers.Select(header => header.Length).ToArray();
            foreach (StatusRow row in rows)
            {
                widths[0] = Math.Max(widths[0], row.Name.Length);
                widths[1] = Math.Max(widths[1], row.Serial.Length);
                widths[2] = Math.Max(widths[2], row.DevicePath.Length);
                for (int channel = 0; channel < row.Channels.Length; channel++)
                    widths[channel + 3] = Math.Max(widths[channel + 3], row.Channels[channel].Length);
            }

            Console.WriteLine(FormatTableRow(headers, widths));
            Console.WriteLine(FormatTableRow(widths.Select(width => new string('-', width)), widths));
            foreach (StatusRow row in rows)
            {
                var values = new List<string> { row.Name, row.Serial, row.DevicePath };
                values.AddRange(row.Channels);
                while (values.Count < headers.Count)
                    values.Add(string.Empty);
                Console.WriteLine(FormatTableRow(values, widths));
            }
        }

        private static string FormatTableRow(IEnumerable<string> values, IReadOnlyList<int> widths)
        {
            return string.Join("  ", values.Select((value, index) => (value ?? string.Empty).PadRight(widths[index])));
        }

        private sealed class StatusRow
        {
            public StatusRow(RelayDevice device)
            {
                Name = device.DisplayName;
                Serial = device.SerialNumber;
                DevicePath = device.DevicePath;
                Channels = Enumerable.Range(1, device.ChannelCount)
                    .Select(channel => device.GetChannelName(channel) + "=" + (device.IsChannelOn(channel) ? "ON" : "OFF"))
                    .ToArray();
            }

            public string Name { get; private set; }
            public string Serial { get; private set; }
            public string DevicePath { get; private set; }
            public string[] Channels { get; private set; }
        }

        private RelayDevice ResolveTargetDevice()
        {
            var matches = relayService.EnumerateDevices()
                .Where(device =>
                    (string.IsNullOrEmpty(target_serial)
                        || string.Equals(device.SerialNumber, target_serial, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrEmpty(target_device_path)
                        || string.Equals(device.DevicePath, target_device_path, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrEmpty(target_device_name)
                        || string.Equals(device.DeviceName, target_device_name, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (matches.Length == 0)
                throw new InvalidOperationException("Relay device not found: " + TargetDescription());
            if (matches.Length > 1)
                throw new InvalidOperationException("Relay device selector is ambiguous: " + TargetDescription());

            return matches[0];
        }

        private string TargetDescription()
        {
            if (!string.IsNullOrEmpty(target_device_path))
                return target_device_path;
            if (!string.IsNullOrEmpty(target_device_name))
                return target_device_name;
            return target_serial;
        }

    }
}

