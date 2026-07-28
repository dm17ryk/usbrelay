using System.Collections.Generic;

namespace usbrelay
{
    public interface IRelayBackend
    {
        IReadOnlyList<RelayDevice> EnumerateDevices();
        RelayDevice GetDevice(string serialNumber);
        void SetChannel(string serialNumber, int channel, bool on);
        void SetChannel(RelayDevice device, int channel, bool on);
    }
}
