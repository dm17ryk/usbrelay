using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace usbrelay
{
    public sealed class NativeUsbRelayBackend : IRelayBackend
    {
        public IReadOnlyList<RelayDevice> EnumerateDevices()
        {
            var devices = new List<RelayDevice>();
            IntPtr current = UsbRelayDeviceHelper.usb_relay_device_enumerate();

            while (current != IntPtr.Zero)
            {
                var info = (UsbRelayDeviceHelper.UsbRelayDeviceInfo)Marshal.PtrToStructure(
                    current,
                    typeof(UsbRelayDeviceHelper.UsbRelayDeviceInfo));

                string serial = info.SerialNumber;
                int channels = Convert.ToInt32(info.Type);
                int status = 0;
                int handle = OpenDevice(serial);

                try
                {
                    if (handle != 0)
                        UsbRelayDeviceHelper.GetStatus(handle, ref status);
                }
                finally
                {
                    if (handle != 0)
                        UsbRelayDeviceHelper.Close(handle);
                }

                devices.Add(new RelayDevice(serial, ConvertType(info.Type), channels, status));
                current = info.Next;
            }

            return devices;
        }

        public RelayDevice GetDevice(string serialNumber)
        {
            foreach (var device in EnumerateDevices())
            {
                if (string.Equals(device.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase))
                    return device;
            }

            throw new InvalidOperationException("Relay device not found: " + serialNumber);
        }

        public void SetChannel(string serialNumber, int channel, bool on)
        {
            int handle = OpenDevice(serialNumber);
            if (handle == 0)
                throw new InvalidOperationException("Failed to open relay device: " + serialNumber);

            try
            {
                int result = on
                    ? UsbRelayDeviceHelper.OpenOneRelayChannel(handle, channel)
                    : UsbRelayDeviceHelper.CloseOneRelayChannel(handle, channel);

                if (result != 0)
                    throw new InvalidOperationException(ChannelOperationError(result));
            }
            finally
            {
                UsbRelayDeviceHelper.Close(handle);
            }
        }

        private static int OpenDevice(string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber))
                return 0;

            return UsbRelayDeviceHelper.OpenWithSerialNumber(serialNumber, serialNumber.Length);
        }

        private static RelayDeviceType ConvertType(UsbRelayDeviceHelper.UsbRelayDeviceType type)
        {
            return (RelayDeviceType)Convert.ToInt32(type);
        }

        private static string ChannelOperationError(int status)
        {
            switch (status)
            {
                case 1:
                    return "Relay operation failed.";
                case 2:
                    return "Relay channel index exceeds the device channel range.";
                default:
                    return "Unknown relay operation error: " + status;
            }
        }
    }
}
