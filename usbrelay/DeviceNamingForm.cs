using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace usbrelay
{
    public sealed class DeviceNamingForm : Form
    {
        private readonly IReadOnlyList<RelayDevice> devices;
        private readonly RelayNamingRepository repository;
        private readonly RelayNamingConfiguration configuration;
        private readonly Dictionary<int, TextBox> channelEditors = new Dictionary<int, TextBox>();
        private ComboBox deviceSelector;
        private TextBox deviceNameEditor;
        private Label devicePathLabel;
        private TableLayoutPanel channelLayout;
        private bool loading;
        private RelayDevice loadedDevice;

        public DeviceNamingForm(IEnumerable<RelayDevice> devices, RelayNamingRepository repository)
        {
            this.devices = (devices ?? Enumerable.Empty<RelayDevice>()).ToList();
            this.repository = repository;
            configuration = repository.Load();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Name USB relay devices and channels";
            Width = 620;
            Height = 560;
            MinimumSize = new Size(520, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            Icon icon = AppAssets.LoadApplicationIcon();
            if (icon != null)
                Icon = icon;

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 5,
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label { Text = "Device", AutoSize = true }, 0, 0);
            deviceSelector = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (RelayDevice device in devices)
                deviceSelector.Items.Add(new DeviceOption(device));
            deviceSelector.SelectedIndexChanged += (s, e) => LoadSelectedDevice();
            root.Controls.Add(deviceSelector, 0, 1);

            var deviceNameRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Dock = DockStyle.Top, AutoSize = true };
            deviceNameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            deviceNameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceNameRow.Controls.Add(new Label { Text = "Friendly name", AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
            deviceNameEditor = new TextBox { Dock = DockStyle.Top };
            deviceNameRow.Controls.Add(deviceNameEditor, 1, 0);
            devicePathLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 4, 0, 6) };
            deviceNameRow.Controls.Add(devicePathLabel, 1, 1);
            root.Controls.Add(deviceNameRow, 0, 2);

            channelLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoScroll = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8)
            };
            channelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            channelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(channelLayout, 0, 3);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
            save.Click += SaveNames;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);
            AcceptButton = save;
            CancelButton = cancel;

            if (deviceSelector.Items.Count > 0)
                deviceSelector.SelectedIndex = 0;
            else
                save.Enabled = false;
        }

        private void LoadSelectedDevice()
        {
            if (loading || deviceSelector.SelectedIndex < 0)
                return;

            if (loadedDevice != null)
                SaveCurrentEntry();
            loading = true;
            RelayDevice selectedDevice = null;
            try
            {
                selectedDevice = ((DeviceOption)deviceSelector.SelectedItem).Device;
                RelayDeviceNaming naming = repository.GetOrCreate(configuration, selectedDevice);
                deviceNameEditor.Text = naming.Name ?? string.Empty;
                devicePathLabel.Text = "Device path: " + selectedDevice.DevicePath;
                channelEditors.Clear();
                channelLayout.Controls.Clear();
                channelLayout.RowStyles.Clear();
                channelLayout.RowCount = selectedDevice.ChannelCount;

                for (int channel = 1; channel <= selectedDevice.ChannelCount; channel++)
                {
                    RelayChannelNaming saved = naming.Channels.FirstOrDefault(item => item.Channel == channel);
                    channelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    channelLayout.Controls.Add(new Label { Text = "CH" + channel, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, channel - 1);
                    var editor = new TextBox { Dock = DockStyle.Top, Text = saved == null ? string.Empty : saved.Name ?? string.Empty };
                    channelEditors[channel] = editor;
                    channelLayout.Controls.Add(editor, 1, channel - 1);
                }
            }
            finally
            {
                loading = false;
                loadedDevice = selectedDevice;
            }
        }

        private void SaveCurrentEntry()
        {
            if (loading || loadedDevice == null)
                return;

            RelayDeviceNaming naming = repository.GetOrCreate(configuration, loadedDevice);
            naming.Name = deviceNameEditor.Text.Trim();
            naming.Channels = channelEditors
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Text))
                .Select(pair => new RelayChannelNaming { Channel = pair.Key, Name = pair.Value.Text.Trim() })
                .ToList();
        }

        private void SaveNames(object sender, EventArgs e)
        {
            SaveCurrentEntry();
            var duplicateNames = configuration.Devices
                .Where(device => !string.IsNullOrWhiteSpace(device.Name))
                .GroupBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateNames.Length > 0)
            {
                MessageBox.Show(this, "Device names must be unique: " + string.Join(", ", duplicateNames), "Duplicate device name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            foreach (RelayDeviceNaming device in configuration.Devices)
            {
                var duplicateChannels = device.Channels
                    .Where(channel => !string.IsNullOrWhiteSpace(channel.Name))
                    .GroupBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                if (duplicateChannels.Length > 0)
                {
                    MessageBox.Show(this, "Channel names must be unique on each device: " + string.Join(", ", duplicateChannels), "Duplicate channel name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
            }

            repository.Save(configuration);
        }

        private sealed class DeviceOption
        {
            public DeviceOption(RelayDevice device) { Device = device; }
            public RelayDevice Device { get; }
            public override string ToString()
            {
                string label = Device.DisplayName + " (" + Device.SerialNumber + ")";
                return string.IsNullOrEmpty(Device.DevicePath) ? label : label + " - " + ShortPath(Device.DevicePath);
            }

            private static string ShortPath(string path)
            {
                return path.Length <= 42 ? path : path.Substring(0, 18) + "..." + path.Substring(path.Length - 18);
            }
        }
    }
}
