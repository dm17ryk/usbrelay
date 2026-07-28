using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace usbrelay
{
    public sealed class NativeUsbRelayBackend : IRelayBackend
    {
        public IReadOnlyList<RelayDevice> EnumerateDevices()
        {
            var devices = new List<RelayDevice>();
            IntPtr enumeration = UsbRelayDeviceHelper.usb_relay_device_enumerate();
            Trace.WriteLine("[NativeUsbRelayBackend] EnumerateDevices: native list=" + enumeration);

            if (enumeration == IntPtr.Zero)
            {
                Trace.WriteLine("[NativeUsbRelayBackend] EnumerateDevices: native list is empty");
                return devices;
            }

            try
            {
                IntPtr current = enumeration;
                while (current != IntPtr.Zero)
                {
                    var info = Marshal.PtrToStructure<UsbRelayDeviceHelper.UsbRelayDeviceInfo>(current);
                    string serial = ReadNativeString(info.SerialNumber);
                    string devicePath = ReadNativeString(info.DevicePath);
                    int channels = info.Type.ToInt32();
                    Trace.WriteLine(
                        "[NativeUsbRelayBackend] EnumerateDevices: node=" + current
                        + ", serial=" + serial
                        + ", path=" + devicePath
                        + ", type=" + channels
                        + ", next=" + info.Next);

                    int status = ReadStatus(ref info, serial, devicePath);
                    devices.Add(new RelayDevice(serial, ConvertType(info.Type), channels, status, devicePath));
                    Trace.WriteLine(
                        "[NativeUsbRelayBackend] EnumerateDevices: added serial=" + serial
                        + ", path=" + devicePath
                        + ", status=" + status);

                    current = info.Next;
                }
            }
            finally
            {
                Trace.WriteLine("[NativeUsbRelayBackend] EnumerateDevices: freeing native list=" + enumeration);
                UsbRelayDeviceHelper.FreeEnumerate(enumeration);
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
            Trace.WriteLine(
                "[NativeUsbRelayBackend] SetChannel(serial): serial=" + serialNumber
                + ", channel=" + channel
                + ", on=" + on);

            RelayDevice[] matches = FindDevicesBySerial(serialNumber);
            if (matches.Length == 0)
            {
                Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(serial): no matching device");
                throw new InvalidOperationException("Relay device not found: " + serialNumber);
            }

            if (matches.Length > 1)
            {
                Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(serial): ambiguous serial, matches=" + matches.Length);
                throw new InvalidOperationException(
                    "Relay serial is shared by " + matches.Length
                    + " connected devices: " + serialNumber
                    + ". Use the GUI device entry or assign unique serial numbers.");
            }

            SetChannel(matches[0], channel, on);
        }

        public void SetChannel(RelayDevice device, int channel, bool on)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            Trace.WriteLine(
                "[NativeUsbRelayBackend] SetChannel(device): serial=" + device.SerialNumber
                + ", path=" + device.DevicePath
                + ", channel=" + channel
                + ", on=" + on);

            IntPtr enumeration = UsbRelayDeviceHelper.usb_relay_device_enumerate();
            if (enumeration == IntPtr.Zero)
            {
                Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(device): native list is empty");
                throw new InvalidOperationException("Relay device not found: " + device.SerialNumber);
            }

            try
            {
                IntPtr current = enumeration;
                UsbRelayDeviceHelper.UsbRelayDeviceInfo selected = default(UsbRelayDeviceHelper.UsbRelayDeviceInfo);
                int matches = 0;

                while (current != IntPtr.Zero)
                {
                    var info = Marshal.PtrToStructure<UsbRelayDeviceHelper.UsbRelayDeviceInfo>(current);
                    string serial = ReadNativeString(info.SerialNumber);
                    string devicePath = ReadNativeString(info.DevicePath);
                    bool serialMatches = string.Equals(serial, device.SerialNumber, StringComparison.OrdinalIgnoreCase);
                    bool pathMatches = string.IsNullOrEmpty(device.DevicePath)
                        || string.Equals(devicePath, device.DevicePath, StringComparison.OrdinalIgnoreCase);

                    Trace.WriteLine(
                        "[NativeUsbRelayBackend] SetChannel(device): inspect node=" + current
                        + ", serial=" + serial
                        + ", path=" + devicePath
                        + ", serialMatches=" + serialMatches
                        + ", pathMatches=" + pathMatches);

                    if (serialMatches && pathMatches)
                    {
                        selected = info;
                        matches++;
                    }

                    current = info.Next;
                }

                if (matches == 0)
                {
                    Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(device): target disappeared");
                    throw new InvalidOperationException("Relay device not found: " + device.SerialNumber);
                }

                if (matches > 1)
                {
                    Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(device): target identity is ambiguous");
                    throw new InvalidOperationException(
                        "Unable to uniquely identify relay device " + device.SerialNumber
                        + ". Assign unique serial numbers and refresh the device list.");
                }

                IntPtr handle = OpenDevice(ref selected);
                if (handle == IntPtr.Zero)
                {
                    Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(device): exact native open failed");
                    throw new InvalidOperationException("Failed to open relay device: " + device.SerialNumber);
                }

                try
                {
                    int result = on
                        ? UsbRelayDeviceHelper.OpenOneRelayChannel(handle, channel)
                        : UsbRelayDeviceHelper.CloseOneRelayChannel(handle, channel);
                    Trace.WriteLine(
                        "[NativeUsbRelayBackend] SetChannel(device): operation result=" + result
                        + ", handle=" + handle);

                    if (result != 0)
                        throw new InvalidOperationException(ChannelOperationError(result));
                }
                finally
                {
                    Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(device): closing handle=" + handle);
                    UsbRelayDeviceHelper.Close(handle);
                }
            }
            finally
            {
                Trace.WriteLine("[NativeUsbRelayBackend] SetChannel(device): freeing native list=" + enumeration);
                UsbRelayDeviceHelper.FreeEnumerate(enumeration);
            }
        }

        private RelayDevice[] FindDevicesBySerial(string serialNumber)
        {
            var matches = new List<RelayDevice>();
            foreach (var device in EnumerateDevices())
            {
                if (string.Equals(device.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase))
                    matches.Add(device);
            }

            return matches.ToArray();
        }

        private static int ReadStatus(
            ref UsbRelayDeviceHelper.UsbRelayDeviceInfo info,
            string serial,
            string devicePath)
        {
            IntPtr handle = OpenDevice(ref info);
            if (handle == IntPtr.Zero)
            {
                Trace.WriteLine(
                    "[NativeUsbRelayBackend] ReadStatus: exact native open failed"
                    + ", serial=" + serial
                    + ", path=" + devicePath);
                return 0;
            }

            try
            {
                int status = 0;
                int result = UsbRelayDeviceHelper.GetStatus(handle, ref status);
                Trace.WriteLine(
                    "[NativeUsbRelayBackend] ReadStatus: serial=" + serial
                    + ", path=" + devicePath
                    + ", result=" + result
                    + ", status=" + status);
                return result == 0 ? status : 0;
            }
            finally
            {
                Trace.WriteLine("[NativeUsbRelayBackend] ReadStatus: closing handle=" + handle);
                UsbRelayDeviceHelper.Close(handle);
            }
        }

        private static IntPtr OpenDevice(ref UsbRelayDeviceHelper.UsbRelayDeviceInfo info)
        {
            Trace.WriteLine("[NativeUsbRelayBackend] OpenDevice(info): path=" + ReadNativeString(info.DevicePath));
            return UsbRelayDeviceHelper.Open(ref info);
        }

        private static string ReadNativeString(IntPtr value)
        {
            return value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(value) ?? string.Empty;
        }

        private static RelayDeviceType ConvertType(IntPtr type)
        {
            return (RelayDeviceType)type.ToInt32();
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
