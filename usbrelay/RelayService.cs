using System.Collections.Generic;

namespace usbrelay
{
    public sealed class RelayService
    {
        private readonly IRelayBackend backend;

        public RelayService(IRelayBackend backend)
        {
            this.backend = backend;
        }

        public IReadOnlyList<RelayDevice> EnumerateDevices()
        {
            return backend.EnumerateDevices();
        }

        public RelayDevice GetDevice(string serialNumber)
        {
            return backend.GetDevice(serialNumber);
        }

        public void TurnOn(string serialNumber, int channel)
        {
            backend.SetChannel(serialNumber, channel, true);
        }

        public void TurnOff(string serialNumber, int channel)
        {
            backend.SetChannel(serialNumber, channel, false);
        }
    }
}
