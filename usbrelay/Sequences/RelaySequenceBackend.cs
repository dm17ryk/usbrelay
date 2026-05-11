namespace usbrelay.Sequences
{
    public sealed class RelaySequenceBackend : ISequenceRelayBackend
    {
        private readonly IRelayBackend backend;

        public RelaySequenceBackend(IRelayBackend backend)
        {
            this.backend = backend;
        }

        public void SetChannel(string serialNumber, int channel, bool on)
        {
            backend.SetChannel(serialNumber, channel, on);
        }

        public bool GetChannelState(string serialNumber, int channel)
        {
            return backend.GetDevice(serialNumber).IsChannelOn(channel);
        }
    }
}
