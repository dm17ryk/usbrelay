using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using usbrelay.Sequences;

namespace usbrelay
{
    public sealed class MainForm : Form
    {
        private readonly IRelayBackend relayBackend;
        private readonly RelayService relayService;
        private readonly SequenceRepository sequenceRepository;
        private readonly SequenceResourceLocks resourceLocks = new SequenceResourceLocks();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly Dictionary<SequenceDefinition, Button> runButtons = new Dictionary<SequenceDefinition, Button>();
        private readonly List<SequenceDefinition> sequences = new List<SequenceDefinition>();

        private FlowLayoutPanel sequenceListPanel;
        private FlowLayoutPanel devicesPanel;
        private SplitContainer splitContainer;
        private TextBox logTextBox;
        private TextBox statusTextBox;
        private SequenceDefinition selectedSequence;
        private bool defaultSplitterApplied;

        public MainForm()
            : this(new NativeUsbRelayBackend(), new SequenceRepository(SequenceRepository.DefaultPath))
        {
        }

        public MainForm(IRelayBackend relayBackend, SequenceRepository sequenceRepository)
        {
            this.relayBackend = relayBackend;
            this.relayService = new RelayService(relayBackend);
            this.sequenceRepository = sequenceRepository;
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadSequences();
            RefreshDevices();
        }

        private void InitializeComponent()
        {
            Text = "USB Relay Control";
            Width = 1180;
            Height = 720;
            MinimumSize = new Size(840, 520);
            Font = new Font("Segoe UI", 9F);

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 6,
                FixedPanel = FixedPanel.None
            };

            splitContainer.Panel1.Controls.Add(CreateSequencePane());
            splitContainer.Panel2.Controls.Add(CreateDevicePane());
            Controls.Add(splitContainer);
            Resize += (s, e) => ResizeDynamicRows();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyDefaultSplitterDistance();
            ResizeDynamicRows();
        }

        private void ApplyDefaultSplitterDistance()
        {
            if (defaultSplitterApplied || splitContainer.Width <= 0)
                return;

            splitContainer.SplitterDistance = Math.Max(splitContainer.Panel1MinSize, (int)(splitContainer.Width * 0.4));
            defaultSplitterApplied = true;
        }

        private Control CreateSequencePane()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            toolbar.Controls.Add(MiniButton("Add", (s, e) => AddSequence()));
            toolbar.Controls.Add(MiniButton("Edit", (s, e) => EditSequence()));
            toolbar.Controls.Add(MiniButton("Remove", (s, e) => RemoveSequence()));
            root.Controls.Add(toolbar, 0, 0);

            sequenceListPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            sequenceListPanel.SizeChanged += (s, e) => ResizeSequenceRows();
            root.Controls.Add(sequenceListPanel, 0, 1);

            root.Controls.Add(new Label { Text = "Sequence log", AutoSize = true, Margin = new Padding(0, 8, 0, 4) }, 0, 2);
            logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(11, 18, 32),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Consolas", 9F)
            };
            root.Controls.Add(logTextBox, 0, 3);

            return root;
        }

        private Control CreateDevicePane()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            toolbar.Controls.Add(MiniButton("Refresh", (s, e) => RefreshDevices()));
            toolbar.Controls.Add(MiniButton("All Off", (s, e) => AllOff()));
            toolbar.Controls.Add(new Label { Text = "Discovered on load, refreshable", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
            root.Controls.Add(toolbar, 0, 0);

            devicesPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            devicesPanel.SizeChanged += (s, e) => ResizeDeviceRows();
            root.Controls.Add(devicesPanel, 0, 1);

            root.Controls.Add(new Label { Text = "Device and channel status", AutoSize = true, Margin = new Padding(0, 8, 0, 4) }, 0, 2);
            statusTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };
            root.Controls.Add(statusTextBox, 0, 3);

            return root;
        }

        private Button MiniButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(2),
                Padding = new Padding(4, 1, 4, 1)
            };
            button.Click += click;
            return button;
        }

        private void LoadSequences()
        {
            sequences.Clear();
            sequences.AddRange(sequenceRepository.Load());
            RenderSequences();
        }

        private void SaveSequences()
        {
            sequenceRepository.Save(sequences);
        }

        private void RenderSequences()
        {
            sequenceListPanel.Controls.Clear();
            runButtons.Clear();

            foreach (var sequence in sequences)
            {
                var row = new Panel
                {
                    Height = 34,
                    Width = SequenceRowWidth(),
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 2, 0, 2),
                    BackColor = ReferenceEquals(sequence, selectedSequence) ? Color.FromArgb(232, 240, 254) : Color.White
                };
                row.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                row.Click += (s, e) => SelectSequence(sequence);
                toolTip.SetToolTip(row, sequence.Description ?? string.Empty);

                var nameButton = MiniButton(sequence.Name, (s, e) => SelectSequence(sequence));
                nameButton.TextAlign = ContentAlignment.MiddleLeft;
                nameButton.UseVisualStyleBackColor = false;
                nameButton.BackColor = row.BackColor;
                nameButton.FlatStyle = FlatStyle.Flat;
                nameButton.FlatAppearance.BorderSize = 0;
                nameButton.AutoSize = false;
                nameButton.Height = 28;
                nameButton.Name = "sequenceNameButton";
                toolTip.SetToolTip(nameButton, sequence.Description ?? string.Empty);

                var run = MiniButton(sequence.DisplayRunButtonText, (s, e) => RunSequence(sequence));
                run.Name = "sequenceRunButton";
                run.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                runButtons[sequence] = run;

                row.Controls.Add(nameButton);
                row.Controls.Add(run);
                LayoutSequenceRow(row);
                sequenceListPanel.Controls.Add(row);
            }

            ResizeSequenceRows();
            UpdateBusyState();
        }

        private void SelectSequence(SequenceDefinition sequence)
        {
            selectedSequence = sequence;
            RenderSequences();
        }

        private void AddSequence()
        {
            using (var form = new SequenceEditorForm(null))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                sequences.Add(form.Sequence);
                selectedSequence = form.Sequence;
                SaveSequences();
                RenderSequences();
            }
        }

        private void EditSequence()
        {
            if (selectedSequence == null)
                return;

            int index = sequences.IndexOf(selectedSequence);
            using (var form = new SequenceEditorForm(selectedSequence))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                sequences[index] = form.Sequence;
                selectedSequence = form.Sequence;
                SaveSequences();
                RenderSequences();
            }
        }

        private void RemoveSequence()
        {
            if (selectedSequence == null)
                return;

            sequences.Remove(selectedSequence);
            selectedSequence = null;
            SaveSequences();
            RenderSequences();
        }

        private async void RunSequence(SequenceDefinition sequence)
        {
            var parsed = SequenceParser.Parse(sequence.Script);
            if (!parsed.IsValid)
            {
                AppendLog(sequence.Name + " validation failed: " + string.Join("; ", parsed.Diagnostics));
                return;
            }

            string owner = Guid.NewGuid().ToString("N");
            if (!resourceLocks.TryReserve(owner, parsed.Resources))
            {
                AppendLog(sequence.Name + " waiting: required channel is busy");
                return;
            }

            UpdateBusyState();
            AppendLog(sequence.Name + " started");
            AppendLog("reserved " + string.Join(", ", parsed.Resources.Select(resource => resource.ToString())));

            try
            {
                var result = await Task.Run(() => SequenceRunner.Run(parsed, new RelaySequenceBackend(relayBackend), new ProcessExternalToolRunner(), skipDelays: false));
                foreach (string line in result.Log)
                    AppendLog(line);
                AppendLog(sequence.Name + (result.Success ? " finished" : " failed"));
            }
            finally
            {
                resourceLocks.Release(owner);
                AppendLog("released " + sequence.Name);
                RefreshDevices();
                UpdateBusyState();
            }
        }

        private void RefreshDevices()
        {
            IReadOnlyList<RelayDevice> devices;
            try
            {
                devices = relayService.EnumerateDevices();
            }
            catch (Exception ex)
            {
                AppendLog("Refresh failed: " + ex.Message);
                return;
            }

            devicesPanel.Controls.Clear();
            statusTextBox.Clear();

            foreach (var device in devices)
            {
                devicesPanel.Controls.Add(CreateDevicePanel(device));
                statusTextBox.AppendText(device.SerialNumber + ": connected, " + device.ChannelCount + " channels, last read " + DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine);
                for (int channel = 1; channel <= device.ChannelCount; channel++)
                    statusTextBox.AppendText("  CH" + channel + " " + (device.IsChannelOn(channel) ? "ON " : "OFF"));
                statusTextBox.AppendText(Environment.NewLine);
            }

            if (devices.Count == 0)
                statusTextBox.Text = "No USB relay devices discovered.";

            ResizeDeviceRows();
        }

        private Control CreateDevicePanel(RelayDevice device)
        {
            var group = new GroupBox
            {
                Text = device.SerialNumber + " - " + device.Type + " - connected",
                Width = DeviceRowWidth(),
                Margin = new Padding(0, 2, 0, 6),
                Tag = device.ChannelCount
            };

            var channels = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true,
                Padding = new Padding(6)
            };

            for (int channel = 1; channel <= device.ChannelCount; channel++)
            {
                int capturedChannel = channel;
                bool on = device.IsChannelOn(channel);
                var button = new Button
                {
                    Text = "● CH" + channel + Environment.NewLine + (on ? "ON" : "OFF"),
                    ForeColor = on ? Color.DarkGreen : Color.Firebrick,
                    Width = 64,
                    Height = 42,
                    Margin = new Padding(2),
                    Tag = new RelayResource(device.SerialNumber, channel)
                };
                button.Click += (s, e) => ToggleChannel(device.SerialNumber, capturedChannel, !on);
                channels.Controls.Add(button);
            }

            group.Controls.Add(channels);
            UpdateDeviceGroupSize(group);
            return group;
        }

        private void ResizeDynamicRows()
        {
            ResizeSequenceRows();
            ResizeDeviceRows();
        }

        private void ResizeSequenceRows()
        {
            foreach (Control row in sequenceListPanel.Controls)
            {
                row.Width = SequenceRowWidth();
                LayoutSequenceRow(row);
            }
        }

        private void LayoutSequenceRow(Control row)
        {
            Control nameButton = row.Controls["sequenceNameButton"];
            Control runButton = row.Controls["sequenceRunButton"];
            if (nameButton == null || runButton == null)
                return;

            runButton.Location = new Point(row.Width - runButton.Width - 4, 3);
            nameButton.Location = new Point(3, 3);
            nameButton.Width = Math.Max(40, runButton.Left - 6);
        }

        private void ResizeDeviceRows()
        {
            foreach (Control row in devicesPanel.Controls)
            {
                row.Width = DeviceRowWidth();
                UpdateDeviceGroupSize(row);
            }
        }

        private void UpdateDeviceGroupSize(Control row)
        {
            if (!(row.Tag is int))
                return;

            int channelCount = (int)row.Tag;
            int usableWidth = Math.Max(64, row.Width - 24);
            int perRow = Math.Max(1, usableWidth / 68);
            int rows = Math.Max(1, (int)Math.Ceiling(channelCount / (double)perRow));
            row.Height = 38 + (rows * 48);
        }

        private int SequenceRowWidth()
        {
            return Math.Max(140, sequenceListPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        }

        private int DeviceRowWidth()
        {
            return Math.Max(260, devicesPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        }

        private void ToggleChannel(string serialNumber, int channel, bool on)
        {
            var resource = new RelayResource(serialNumber, channel);
            if (resourceLocks.IsBusy(resource))
                return;

            try
            {
                relayBackend.SetChannel(serialNumber, channel, on);
                AppendLog(serialNumber + " CH" + channel + " -> " + (on ? "ON" : "OFF") + " ok");
                RefreshDevices();
            }
            catch (Exception ex)
            {
                AppendLog(serialNumber + " CH" + channel + " failed: " + ex.Message);
            }
        }

        private void AllOff()
        {
            foreach (var device in relayService.EnumerateDevices())
            {
                for (int channel = 1; channel <= device.ChannelCount; channel++)
                    ToggleChannel(device.SerialNumber, channel, false);
            }
        }

        private void UpdateBusyState()
        {
            foreach (var pair in runButtons)
            {
                var parsed = SequenceParser.Parse(pair.Key.Script);
                pair.Value.Enabled = parsed.IsValid && parsed.Resources.All(resource => !resourceLocks.IsBusy(resource));
            }

            foreach (Control group in devicesPanel.Controls)
            {
                foreach (Control child in group.Controls)
                {
                    foreach (Control button in child.Controls)
                    {
                        if (button.Tag is RelayResource)
                            button.Enabled = !resourceLocks.IsBusy((RelayResource)button.Tag);
                    }
                }
            }
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            logTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
    }
}
