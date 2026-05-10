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
                    states[new RelayResource(device.SerialNumber, channel)] = device.IsChannelOn(channel);
            }
        }

        public IReadOnlyList<RelayDevice> EnumerateDevices()
        {
            return devices.Values.Select(CloneWithState).ToList();
        }

        public RelayDevice GetDevice(string serialNumber)
        {
            return CloneWithState(devices[serialNumber]);
        }

        public void SetChannel(string serialNumber, int channel, bool on)
        {
            states[new RelayResource(serialNumber, channel)] = on;
        }

        public bool GetChannelState(string serialNumber, int channel)
        {
            bool value;
            return states.TryGetValue(new RelayResource(serialNumber, channel), out value) && value;
        }

        private RelayDevice CloneWithState(RelayDevice device)
        {
            int mask = 0;
            for (int channel = 1; channel <= device.ChannelCount; channel++)
            {
                if (GetChannelState(device.SerialNumber, channel))
                    mask |= 1 << (channel - 1);
            }

            return new RelayDevice(device.SerialNumber, device.Type, device.ChannelCount, mask);
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
