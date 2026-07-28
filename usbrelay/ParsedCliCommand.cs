using System.Collections.Generic;
using System.Linq;

namespace usbrelay
{
    internal sealed class ParsedCliCommand
    {
        public ParsedCliCommand(
            Operations operation,
            string serial,
            string devicePath,
            string deviceName,
            string setDeviceName,
            IEnumerable<string> setChannelNamePairs,
            IEnumerable<int> onChannels,
            IEnumerable<string> onChannelNames,
            IEnumerable<int> offChannels,
            IEnumerable<string> offChannelNames,
            bool isGui,
            bool isHelpRequested,
            bool isVersionRequested,
            bool hasSystemCommandLineErrors,
            IEnumerable<string> errors)
        {
            Operation = operation;
            Serial = serial;
            DevicePath = devicePath;
            DeviceName = deviceName;
            SetDeviceName = setDeviceName;
            SetChannelNamePairs = setChannelNamePairs.ToArray();
            OnChannels = onChannels.ToArray();
            OnChannelNames = onChannelNames.ToArray();
            OffChannels = offChannels.ToArray();
            OffChannelNames = offChannelNames.ToArray();
            IsGui = isGui;
            IsHelpRequested = isHelpRequested;
            IsVersionRequested = isVersionRequested;
            HasSystemCommandLineErrors = hasSystemCommandLineErrors;
            Errors = errors.ToArray();
        }

        public Operations Operation { get; private set; }
        public string Serial { get; private set; }
        public string DevicePath { get; private set; }
        public string DeviceName { get; private set; }
        public string SetDeviceName { get; private set; }
        public IEnumerable<string> SetChannelNamePairs { get; private set; }
        public IEnumerable<int> OnChannels { get; private set; }
        public IEnumerable<string> OnChannelNames { get; private set; }
        public IEnumerable<int> OffChannels { get; private set; }
        public IEnumerable<string> OffChannelNames { get; private set; }
        public bool IsGui { get; private set; }
        public bool IsHelpRequested { get; private set; }
        public bool IsVersionRequested { get; private set; }
        public bool HasSystemCommandLineErrors { get; private set; }
        public IEnumerable<string> Errors { get; private set; }
        public bool IsValid { get { return !Errors.Any(); } }

        public bool HasRelayOptions
        {
            get
            {
                return Operation != Operations.NULL
                    || !string.IsNullOrEmpty(Serial)
                    || !string.IsNullOrEmpty(DevicePath)
                    || !string.IsNullOrEmpty(DeviceName)
                    || !string.IsNullOrEmpty(SetDeviceName)
                    || SetChannelNamePairs.Any()
                    || OnChannels.Any()
                    || OnChannelNames.Any()
                    || OffChannels.Any()
                    || OffChannelNames.Any();
            }
        }
    }
}
