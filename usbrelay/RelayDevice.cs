using System;

namespace usbrelay
{
    public sealed class RelayDevice
    {
        public RelayDevice(string serialNumber, RelayDeviceType type, int channelCount, int statusMask)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("Serial number is required.", nameof(serialNumber));

            SerialNumber = serialNumber;
            Type = type;
            ChannelCount = channelCount;
            StatusMask = statusMask;
        }

        public string SerialNumber { get; }
        public RelayDeviceType Type { get; }
        public int ChannelCount { get; }
        public int StatusMask { get; }

        public bool IsChannelOn(int channel)
        {
            if (channel < 1 || channel > ChannelCount)
                return false;

            return (StatusMask & (1 << (channel - 1))) != 0;
        }
    }
}
