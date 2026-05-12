using System.Collections.Generic;
using System.Linq;

namespace usbrelay
{
    internal sealed class ParsedCliCommand
    {
        public ParsedCliCommand(
            Operations operation,
            string serial,
            IEnumerable<int> onChannels,
            IEnumerable<int> offChannels,
            bool isGui,
            bool isHelpRequested,
            bool isVersionRequested,
            bool hasSystemCommandLineErrors,
            IEnumerable<string> errors)
        {
            Operation = operation;
            Serial = serial;
            OnChannels = onChannels.ToArray();
            OffChannels = offChannels.ToArray();
            IsGui = isGui;
            IsHelpRequested = isHelpRequested;
            IsVersionRequested = isVersionRequested;
            HasSystemCommandLineErrors = hasSystemCommandLineErrors;
            Errors = errors.ToArray();
        }

        public Operations Operation { get; private set; }
        public string Serial { get; private set; }
        public IEnumerable<int> OnChannels { get; private set; }
        public IEnumerable<int> OffChannels { get; private set; }
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
                    || OnChannels.Any()
                    || OffChannels.Any();
            }
        }
    }
}
