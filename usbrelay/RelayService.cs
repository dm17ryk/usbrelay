using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace usbrelay
{
    public sealed class RelayService
    {
        private readonly IRelayBackend backend;
        private readonly RelayNamingRepository namingRepository;
        private readonly object syncRoot = new object();

        public RelayService(IRelayBackend backend)
            : this(backend, new RelayNamingRepository(RelayNamingRepository.DefaultPath))
        {
        }

        public RelayService(IRelayBackend backend, RelayNamingRepository namingRepository)
        {
            this.backend = backend;
            this.namingRepository = namingRepository;
        }

        public IReadOnlyList<RelayDevice> EnumerateDevices()
        {
            lock (syncRoot)
                return namingRepository.Apply(backend.EnumerateDevices());
        }

        public RelayDevice GetDevice(string selector)
        {
            lock (syncRoot)
            {
                var matches = EnumerateDevices()
                    .Where(device => device.MatchesSelector(selector))
                    .ToArray();
                if (matches.Length == 0)
                    throw new InvalidOperationException("Relay device not found: " + selector);
                if (matches.Length > 1)
                    throw new InvalidOperationException("Relay device selector is ambiguous: " + selector);
                return matches[0];
            }
        }

        public void TurnOn(string selector, int channel)
        {
            TurnOn(GetDevice(selector), channel);
        }

        public void TurnOn(RelayDevice device, int channel)
        {
            SetChannel(device, channel, true);
        }

        public void TurnOff(string selector, int channel)
        {
            TurnOff(GetDevice(selector), channel);
        }

        public void TurnOff(RelayDevice device, int channel)
        {
            SetChannel(device, channel, false);
        }

        public void SetChannel(RelayDevice device, int channel, bool on)
        {
            lock (syncRoot)
                ExecuteWithTransientDeviceRetry(() => backend.SetChannel(device, channel, on));
        }

        public void SetChannel(string selector, int channel, bool on)
        {
            lock (syncRoot)
                ExecuteWithTransientDeviceRetry(() => backend.SetChannel(GetDevice(selector), channel, on));
        }

        public IReadOnlyList<string> AllOff(Action<string> log = null)
        {
            var errors = new List<string>();
            foreach (RelayDevice device in EnumerateDevices())
            {
                for (int channel = 1; channel <= device.ChannelCount; channel++)
                {
                    string target = device.DisplayName + " [" + device.DevicePath + "] CH" + channel;
                    try
                    {
                        Trace.WriteLine("[RelayService] AllOff switching " + target);
                        SetChannel(device, channel, false);
                        log?.Invoke(target + " -> OFF ok");
                    }
                    catch (Exception ex)
                    {
                        string message = target + " failed: " + ex.Message;
                        Trace.WriteLine("[RelayService] AllOff " + message);
                        errors.Add(message);
                        log?.Invoke(message);
                    }
                }
            }
            Trace.WriteLine("[RelayService] AllOff completed; failures=" + errors.Count);
            return errors;
        }

        public void UpdateNames(string selector, string deviceName, IEnumerable<string> channelNamePairs)
        {
            lock (syncRoot)
                UpdateNames(GetDevice(selector), deviceName, channelNamePairs);
        }

        public void UpdateNames(RelayDevice device, string deviceName, IEnumerable<string> channelNamePairs)
        {
            lock (syncRoot)
                UpdateNamesCore(device, deviceName, channelNamePairs);
        }

        private void UpdateNamesCore(RelayDevice device, string deviceName, IEnumerable<string> channelNamePairs)
        {
            var configuration = namingRepository.Load();
            RelayDeviceNaming naming = namingRepository.GetOrCreate(configuration, device);
            if (deviceName != null)
                naming.Name = deviceName.Trim();

            naming.Channels = naming.Channels ?? new List<RelayChannelNaming>();
            string[] pairs = (channelNamePairs ?? Enumerable.Empty<string>()).ToArray();
            if ((pairs.Length % 2) != 0)
                throw new InvalidOperationException("--set-channel-name requires channel/name pairs.");

            for (int index = 0; index < pairs.Length; index += 2)
            {
                string channelSelector = pairs[index].Trim();
                string channelName = pairs[index + 1].Trim();
                int channel = device.ResolveChannel(channelSelector);
                RelayChannelNaming existing = naming.Channels.FirstOrDefault(item => item.Channel == channel);
                if (channelName.Length == 0)
                {
                    Trace.WriteLine("[RelayService] Clearing channel name; device=" + device.DevicePath + ", channel=" + channel);
                    if (existing != null) naming.Channels.Remove(existing);
                    continue;
                }
                if (existing == null)
                    naming.Channels.Add(new RelayChannelNaming { Channel = channel, Name = channelName });
                else
                    existing.Name = channelName;
            }

            var duplicateDeviceNames = configuration.Devices
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateDeviceNames.Length > 0)
                throw new InvalidOperationException("Device names must be unique: " + string.Join(", ", duplicateDeviceNames));

            var duplicateChannelNames = naming.Channels
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateChannelNames.Length > 0)
                throw new InvalidOperationException("Channel names must be unique on each device: " + string.Join(", ", duplicateChannelNames));

            naming.Name = naming.Name == null ? string.Empty : naming.Name.Trim();
            foreach (RelayChannelNaming channel in naming.Channels)
                channel.Name = channel.Name == null ? string.Empty : channel.Name.Trim();
            namingRepository.Save(configuration);
        }

        private static void ExecuteWithTransientDeviceRetry(Action operation)
        {
            const int maxAttempts = 3;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    operation();
                    return;
                }
                catch (Exception ex)
                {
                    if (!IsTransientDeviceFailure(ex))
                        throw;

                    lastException = ex;
                    Trace.WriteLine(
                        "[RelayService] transient device lookup failure; attempt=" + attempt
                        + "/" + maxAttempts + ", error=" + ex.Message);
                    if (attempt < maxAttempts)
                        Thread.Sleep(75 * attempt);
                }
            }

            throw lastException;
        }

        private static bool IsTransientDeviceFailure(Exception exception)
        {
            return exception is InvalidOperationException
                && exception.Message.IndexOf("Relay device not found", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
