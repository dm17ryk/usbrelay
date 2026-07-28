namespace usbrelay.Sequences
{
    public sealed class RelaySequenceBackend : ISequenceRelayBackend, ISequenceRelayDisplay
    {
        private readonly RelayService relayService;

        public RelaySequenceBackend(IRelayBackend backend)
        {
            relayService = new RelayService(backend);
        }

        public RelaySequenceBackend(RelayService relayService)
        {
            this.relayService = relayService;
        }

        public void SetChannel(string serialNumber, int channel, bool on)
        {
            relayService.SetChannel(serialNumber, channel, on);
        }

        public void SetChannel(string deviceSelector, string channelName, bool on)
        {
            RelayDevice device = relayService.GetDevice(deviceSelector);
            relayService.SetChannel(device, device.ResolveChannel(channelName), on);
        }

        public bool GetChannelState(string serialNumber, int channel)
        {
            return relayService.GetDevice(serialNumber).IsChannelOn(channel);
        }

        public bool GetChannelState(string deviceSelector, string channelName)
        {
            RelayDevice device = relayService.GetDevice(deviceSelector);
            return device.IsChannelOn(device.ResolveChannel(channelName));
        }

        public string DescribeChannel(string deviceSelector, int channel)
        {
            RelayDevice device = relayService.GetDevice(deviceSelector);
            return device.DisplayName + " " + device.GetChannelName(channel) + " (CH" + channel + ")";
        }

        public string DescribeChannel(string deviceSelector, string channelName)
        {
            RelayDevice device = relayService.GetDevice(deviceSelector);
            int channel = device.ResolveChannel(channelName);
            return device.DisplayName + " " + device.GetChannelName(channel) + " (CH" + channel + ")";
        }
    }
}
