using System;

namespace usbrelay.Sequences
{
    public struct RelayResource : IEquatable<RelayResource>
    {
        public RelayResource(string serialNumber, int channel)
            : this(serialNumber, channel, null)
        {
        }

        public RelayResource(string serialNumber, int channel, string devicePath)
            : this(serialNumber, channel, null, devicePath)
        {
        }

        public RelayResource(string serialNumber, string channelName)
            : this(serialNumber, 0, channelName, null)
        {
        }

        private RelayResource(string serialNumber, int channel, string channelName, string devicePath)
        {
            SerialNumber = serialNumber ?? string.Empty;
            Channel = channel;
            ChannelName = channelName ?? string.Empty;
            DevicePath = devicePath ?? string.Empty;
        }

        public string SerialNumber { get; }
        public int Channel { get; }
        public string ChannelName { get; }
        public string DevicePath { get; }

        public bool Equals(RelayResource other)
        {
            return string.Equals(SerialNumber, other.SerialNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(DevicePath, other.DevicePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ChannelName, other.ChannelName, StringComparison.OrdinalIgnoreCase)
                && Channel == other.Channel;
        }

        public override bool Equals(object obj)
        {
            return obj is RelayResource && Equals((RelayResource)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(SerialNumber)
                ^ StringComparer.OrdinalIgnoreCase.GetHashCode(DevicePath)
                ^ StringComparer.OrdinalIgnoreCase.GetHashCode(ChannelName)
                ^ Channel.GetHashCode();
        }

        public override string ToString()
        {
            return SerialNumber + " " + (string.IsNullOrEmpty(ChannelName) ? "CH" + Channel : ChannelName);
        }
    }
}
