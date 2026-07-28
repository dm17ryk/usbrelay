using System;
using System.Collections.Generic;
using System.Linq;
using usbrelay.Sequences;

namespace usbrelay.Tests
{
    internal sealed class FakeRelayBackend : IRelayBackend, ISequenceRelayBackend
    {
        private readonly Dictionary<string, RelayDevice> devices;
        private readonly Dictionary<RelayResource, bool> states = new Dictionary<RelayResource, bool>();

        public FakeRelayBackend(params RelayDevice[] devices)
        {
            this.devices = devices.ToDictionary(device => device.SerialNumber, StringComparer.OrdinalIgnoreCase);
            foreach (var device in devices)
            {
                for (int channel = 1; channel <= device.ChannelCount; channel++)
                    states[new RelayResource(device.SerialNumber, channel, device.DevicePath)] = device.IsChannelOn(channel);
            }
        }

        public IReadOnlyList<RelayDevice> EnumerateDevices()
        {
            EnumerateDevicesCallCount++;
            return devices.Values.Select(CloneWithState).ToList();
        }

        public int EnumerateDevicesCallCount { get; private set; }

        public RelayDevice GetDevice(string serialNumber)
        {
            return CloneWithState(devices[serialNumber]);
        }

        public void SetChannel(string serialNumber, int channel, bool on)
        {
            states[new RelayResource(serialNumber, channel)] = on;
        }

        public void SetChannel(RelayDevice device, int channel, bool on)
        {
            states[new RelayResource(device.SerialNumber, channel, device.DevicePath)] = on;
        }

        public void SetChannel(string deviceSelector, string channelName, bool on)
        {
            RelayDevice device = GetDevice(deviceSelector);
            SetChannel(device, device.ResolveChannel(channelName), on);
        }

        public bool GetChannelState(string serialNumber, int channel)
        {
            return states.Any(pair =>
                string.Equals(pair.Key.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase)
                && pair.Key.Channel == channel
                && pair.Value);
        }

        public bool GetChannelState(string deviceSelector, string channelName)
        {
            RelayDevice device = GetDevice(deviceSelector);
            return GetChannelState(device.SerialNumber, device.ResolveChannel(channelName));
        }

        private RelayDevice CloneWithState(RelayDevice device)
        {
            int mask = 0;
            for (int channel = 1; channel <= device.ChannelCount; channel++)
            {
                bool isOn;
                if (states.TryGetValue(new RelayResource(device.SerialNumber, channel, device.DevicePath), out isOn) && isOn)
                    mask |= 1 << (channel - 1);
            }

            return new RelayDevice(
                device.SerialNumber,
                device.Type,
                device.ChannelCount,
                mask,
                device.DevicePath,
                device.DeviceName,
                device.ChannelNames.ToDictionary(pair => pair.Key, pair => pair.Value));
        }
    }

    internal sealed class FakeExternalToolRunner : IExternalToolRunner
    {
        private readonly string output;
        private readonly int exitCode;

        public FakeExternalToolRunner(string output, int exitCode = 0)
        {
            this.output = output;
            this.exitCode = exitCode;
        }

        public ExternalToolResult Run(string path, string arguments)
        {
            return new ExternalToolResult(exitCode, output);
        }
    }
}
