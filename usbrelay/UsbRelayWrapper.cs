//  
//  USB-Relay Utility
//      - A generic tool to handle common 1-8 channel USB-Relay boards.
//      - Developd for Astro-Photograpy Equipment Power Control.
//
//  Author: Min Xie (minxie.dallas@gmail.com)
//

using System;
using System.Collections.Generic;

namespace usbrelay
{
    class UsbRelayWrapper
    {
        private string target_serial { get; set; }
        private readonly RelayService relayService;

        public UsbRelayWrapper(string serial)
            : this(serial, new RelayService(new NativeUsbRelayBackend()))
        {
        }

        public UsbRelayWrapper(string serial, RelayService relayService)
        {
            target_serial = serial;
            this.relayService = relayService;
        }
        ~UsbRelayWrapper() { }

        public int open_device_handle(string serial)
        {
            if (serial != "")
            {
                int retval = UsbRelayDeviceHelper.OpenWithSerialNumber(serial, serial.Length);
                return retval;
            }
            return 0;
        }

        public void on_off_channels(HashSet<int> on_channels, HashSet<int> off_channels)
        {
            foreach (int i in on_channels)
            {
                Console.WriteLine(String.Format("Turn on channel {0} on {1}: {2}", i, target_serial, run_channel_operation(i, true)));
            }
            foreach (int i in off_channels)
            {
                Console.WriteLine(String.Format("Turn off channel {0} on {1}: {2}", i, target_serial, run_channel_operation(i, false)));
            }

            Console.WriteLine();
            status();
        }

        private string run_channel_operation(int channel, bool on)
        {
            try
            {
                if (on)
                    relayService.TurnOn(target_serial, channel);
                else
                    relayService.TurnOff(target_serial, channel);
                return "success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public void list()
        {
            var devices = relayService.EnumerateDevices();
            if (devices.Count > 0)
            {
                Console.WriteLine("Serial    Type");
                Console.WriteLine("------    ----");
            }

            foreach (var device in devices)
            {
                Console.WriteLine(String.Format("{0}     {1}", device.SerialNumber, device.Type));
            }
        }

        public void status()
        {
            var devices = relayService.EnumerateDevices();
            if (devices.Count > 0)
            {
                Console.Write("Serial  ");
                for (int channel = 1; channel <= 8; channel++) Console.Write(String.Format(" C{0}  ", channel));
                Console.WriteLine();
                Console.Write("------  ");
                for (int channel = 0; channel < 8; channel++) Console.Write(String.Format("---- ", channel));
                Console.WriteLine();
            }

            foreach (var device in devices)
            {
                string serial = device.SerialNumber;
                Console.Write(String.Format("{0}   ", serial));

                for (int channel = 1; channel <= device.ChannelCount; channel++)
                {
                    if (device.IsChannelOn(channel))
                        Console.Write("ON   ");
                    else
                        Console.Write("OFF  ");
                }
                Console.WriteLine();
            }
        }

    }
}

