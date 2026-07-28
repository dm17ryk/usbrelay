using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace usbrelay
{
    public sealed class RelayDevice
    {
        public RelayDevice(string serialNumber, RelayDeviceType type, int channelCount, int statusMask)
            : this(serialNumber, type, channelCount, statusMask, null, null, null)
        {
        }

        public RelayDevice(string serialNumber, RelayDeviceType type, int channelCount, int statusMask, string devicePath)
            : this(serialNumber, type, channelCount, statusMask, devicePath, null, null)
        {
        }

        public RelayDevice(
            string serialNumber,
            RelayDeviceType type,
            int channelCount,
            int statusMask,
            string devicePath,
            string deviceName,
            IDictionary<int, string> channelNames)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("Serial number is required.", nameof(serialNumber));

            SerialNumber = serialNumber;
            Type = type;
            ChannelCount = channelCount;
            StatusMask = statusMask;
            DevicePath = devicePath ?? string.Empty;
            DeviceName = deviceName ?? string.Empty;
            var copiedChannelNames = new Dictionary<int, string>();
            if (channelNames != null)
            {
                foreach (var pair in channelNames)
                {
                    if (pair.Key >= 1 && pair.Key <= channelCount && !string.IsNullOrWhiteSpace(pair.Value))
                        copiedChannelNames[pair.Key] = pair.Value.Trim();
                }
            }
            ChannelNames = new ReadOnlyDictionary<int, string>(copiedChannelNames);
        }

        public string SerialNumber { get; }
        public RelayDeviceType Type { get; }
        public int ChannelCount { get; }
        public int StatusMask { get; }
        public string DevicePath { get; }
        public string DeviceName { get; }
        public IReadOnlyDictionary<int, string> ChannelNames { get; }
        public string DisplayName { get { return string.IsNullOrEmpty(DeviceName) ? SerialNumber : DeviceName; } }

        public bool IsChannelOn(int channel)
        {
            if (channel < 1 || channel > ChannelCount)
                return false;

            return (StatusMask & (1 << (channel - 1))) != 0;
        }

        public string GetChannelName(int channel)
        {
            string name;
            return ChannelNames.TryGetValue(channel, out name) ? name : "CH" + channel;
        }

        public int ResolveChannel(string selector)
        {
            int channel;
            if (int.TryParse(selector, out channel))
            {
                ValidateChannel(channel, selector);
                return channel;
            }

            foreach (var pair in ChannelNames)
            {
                if (string.Equals(pair.Value, selector, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }

            throw new InvalidOperationException(
                "Channel not found on " + DisplayName + ": " + selector);
        }

        public bool MatchesSelector(string selector)
        {
            return string.Equals(SerialNumber, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(DevicePath, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(DeviceName, selector, StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateChannel(int channel, string selector)
        {
            if (channel < 1 || channel > ChannelCount)
                throw new InvalidOperationException(
                    "Channel " + selector + " exceeds the channel range of " + DisplayName + ".");
        }
    }
}
