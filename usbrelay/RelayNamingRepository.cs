using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace usbrelay
{
    [DataContract]
    public sealed class RelayNamingConfiguration
    {
        [DataMember(Order = 1)]
        public List<RelayDeviceNaming> Devices { get; set; } = new List<RelayDeviceNaming>();
    }

    [DataContract]
    public sealed class RelayDeviceNaming
    {
        [DataMember(Order = 1)]
        public string DeviceKey { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }

        [DataMember(Order = 3)]
        public List<RelayChannelNaming> Channels { get; set; } = new List<RelayChannelNaming>();
    }

    [DataContract]
    public sealed class RelayChannelNaming
    {
        [DataMember(Order = 1)]
        public int Channel { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }
    }

    public sealed class RelayNamingRepository
    {
        private readonly string path;

        public RelayNamingRepository(string path)
        {
            this.path = path;
        }

        public static string DefaultPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "usbrelay", "device-names.json");
            }
        }

        public RelayNamingConfiguration Load()
        {
            if (!File.Exists(path))
                return new RelayNamingConfiguration();

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(RelayNamingConfiguration));
                    var configuration = (RelayNamingConfiguration)serializer.ReadObject(stream);
                    if (configuration == null)
                        return new RelayNamingConfiguration();
                    configuration.Devices = configuration.Devices ?? new List<RelayDeviceNaming>();
                    foreach (var device in configuration.Devices)
                        device.Channels = device.Channels ?? new List<RelayChannelNaming>();
                    Trace.WriteLine("[RelayNamingRepository] Loaded " + configuration.Devices.Count + " device naming entries from " + path);
                    return configuration;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[RelayNamingRepository] Failed to load " + path + ": " + ex);
                return new RelayNamingConfiguration();
            }
        }

        public void Save(RelayNamingConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(RelayNamingConfiguration));
                serializer.WriteObject(stream, configuration);
            }

            Trace.WriteLine("[RelayNamingRepository] Saved " + configuration.Devices.Count + " device naming entries to " + path);
        }

        public IReadOnlyList<RelayDevice> Apply(IEnumerable<RelayDevice> devices)
        {
            var configuration = Load();
            return devices.Select(device => Apply(device, configuration)).ToList();
        }

        public RelayDevice Apply(RelayDevice device, RelayNamingConfiguration configuration)
        {
            RelayDeviceNaming naming = Find(configuration, GetDeviceKey(device));
            if (naming == null)
                return device;

            var channelNames = naming.Channels
                .Where(channel => channel != null && !string.IsNullOrWhiteSpace(channel.Name))
                .GroupBy(channel => channel.Channel)
                .ToDictionary(group => group.Key, group => group.First().Name.Trim());
            return new RelayDevice(
                device.SerialNumber,
                device.Type,
                device.ChannelCount,
                device.StatusMask,
                device.DevicePath,
                naming.Name,
                channelNames);
        }

        public RelayDeviceNaming GetOrCreate(RelayNamingConfiguration configuration, RelayDevice device)
        {
            configuration.Devices = configuration.Devices ?? new List<RelayDeviceNaming>();
            string key = GetDeviceKey(device);
            RelayDeviceNaming naming = Find(configuration, key);
            if (naming != null)
                return naming;

            naming = new RelayDeviceNaming { DeviceKey = key };
            configuration.Devices.Add(naming);
            return naming;
        }

        public static string GetDeviceKey(RelayDevice device)
        {
            if (!string.IsNullOrEmpty(device.DevicePath))
                return device.DevicePath;
            return "serial:" + device.SerialNumber;
        }

        private static RelayDeviceNaming Find(RelayNamingConfiguration configuration, string key)
        {
            return configuration.Devices.FirstOrDefault(device =>
                device != null && string.Equals(device.DeviceKey, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
