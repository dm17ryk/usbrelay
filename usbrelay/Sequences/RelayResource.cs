using System;

namespace usbrelay.Sequences
{
    public struct RelayResource : IEquatable<RelayResource>
    {
        public RelayResource(string serialNumber, int channel)
        {
            SerialNumber = serialNumber ?? string.Empty;
            Channel = channel;
        }

        public string SerialNumber { get; }
        public int Channel { get; }

        public bool Equals(RelayResource other)
        {
            return string.Equals(SerialNumber, other.SerialNumber, StringComparison.OrdinalIgnoreCase)
                && Channel == other.Channel;
        }

        public override bool Equals(object obj)
        {
            return obj is RelayResource && Equals((RelayResource)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(SerialNumber) ^ Channel.GetHashCode();
        }

        public override string ToString()
        {
            return SerialNumber + " CH" + Channel;
        }
    }
}
