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
        private readonly string layoutSettingsPath;
        private readonly SequenceResourceLocks resourceLocks = new SequenceResourceLocks();
        private readonly List<SequenceDefinition> sequences = new List<SequenceDefinition>();

        private DataGridView sequenceGrid;
        private FlowLayoutPanel devicesPanel;
        private SplitContainer splitContainer;
        private TableLayoutPanel sequencePaneLayout;
        private TableLayoutPanel devicePaneLayout;
        private TextBox logTextBox;
        private TextBox statusTextBox;
        private SequenceDefinition selectedSequence;
        private bool defaultSplitterApplied;

        public MainForm()
            : this(new NativeUsbRelayBackend(), new SequenceRepository(SequenceRepository.DefaultPath), MainLayoutSettings.DefaultPath)
        {
        }

        public MainForm(IRelayBackend relayBackend, SequenceRepository sequenceRepository)
            : this(relayBackend, sequenceRepository, null)
        {
        }

        public MainForm(IRelayBackend relayBackend, SequenceRepository sequenceRepository, string layoutSettingsPath)
        {
            this.relayBackend = relayBackend;
            this.relayService = new RelayService(relayBackend);
            this.sequenceRepository = sequenceRepository;
            this.layoutSettingsPath = layoutSettingsPath;
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadLayoutSettings();
            LoadSequences();
            RefreshDevices();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyDefaultSplitterDistance();
            ResizeDeviceRows();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveLayoutSettings();
            base.OnFormClosing(e);
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
            Resize += (s, e) => ResizeDeviceRows();
        }

        private Control CreateSequencePane()
        {
            sequencePaneLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            toolbar.Controls.Add(MiniButton("Add", (s, e) => AddSequence()));
            toolbar.Controls.Add(MiniButton("Edit", (s, e) => EditSequence()));
            toolbar.Controls.Add(MiniButton("Remove", (s, e) => RemoveSequence()));
            sequencePaneLayout.Controls.Add(toolbar, 0, 0);

            sequenceGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                ColumnHeadersVisible = false,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Vertical,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle
            };
            sequenceGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NameColumn", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            sequenceGrid.Columns.Add(new DataGridViewButtonColumn { Name = "RunColumn", Width = 86, UseColumnTextForButtonValue = false });
            sequenceGrid.CellClick += SequenceGrid_CellClick;
            sequenceGrid.SelectionChanged += (s, e) => SelectGridSequence();
            sequenceGrid.CellToolTipTextNeeded += SequenceGrid_CellToolTipTextNeeded;
            sequencePaneLayout.Controls.Add(sequenceGrid, 0, 1);

            sequencePaneLayout.Controls.Add(new Label { Text = "Sequence log", AutoSize = true, Margin = new Padding(0, 8, 0, 4) }, 0, 2);
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
            sequencePaneLayout.Controls.Add(logTextBox, 0, 3);

            return sequencePaneLayout;
        }

        private Control CreateDevicePane()
        {
            devicePaneLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            toolbar.Controls.Add(MiniButton("Refresh", (s, e) => RefreshDevices()));
            toolbar.Controls.Add(MiniButton("All Off", (s, e) => AllOff()));
            toolbar.Controls.Add(new Label { Text = "Discovered on load, refreshable", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
            devicePaneLayout.Controls.Add(toolbar, 0, 0);

            devicesPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            devicesPanel.SizeChanged += (s, e) => ResizeDeviceRows();
            devicePaneLayout.Controls.Add(devicesPanel, 0, 1);

            devicePaneLayout.Controls.Add(new Label { Text = "Device and channel status", AutoSize = true, Margin = new Padding(0, 8, 0, 4) }, 0, 2);
            statusTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };
            devicePaneLayout.Controls.Add(statusTextBox, 0, 3);

            return devicePaneLayout;
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

        private void ApplyDefaultSplitterDistance()
        {
            if (defaultSplitterApplied || splitContainer.Width <= 0 || splitContainer.SplitterDistance > 0)
                return;

            splitContainer.SplitterDistance = Math.Max(splitContainer.Panel1MinSize, (int)(splitContainer.Width * 0.4));
            defaultSplitterApplied = true;
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
            sequenceGrid.Rows.Clear();

            foreach (var sequence in sequences)
            {
                int rowIndex = sequenceGrid.Rows.Add(sequence.Name, sequence.DisplayRunButtonText);
                var row = sequenceGrid.Rows[rowIndex];
                row.Tag = sequence;
                row.Height = 28;
                foreach (DataGridViewCell cell in row.Cells)
                    cell.ToolTipText = sequence.Description ?? string.Empty;
                if (ReferenceEquals(sequence, selectedSequence))
                    row.Selected = true;
            }

            UpdateBusyState();
        }

        private void SelectGridSequence()
        {
            if (sequenceGrid.SelectedRows.Count == 0)
                return;

            selectedSequence = sequenceGrid.SelectedRows[0].Tag as SequenceDefinition;
        }

        private void SequenceGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= sequenceGrid.Rows.Count)
                return;

            var sequence = sequenceGrid.Rows[e.RowIndex].Tag as SequenceDefinition;
            if (sequence == null)
                return;

            selectedSequence = sequence;
            if (sequenceGrid.Columns[e.ColumnIndex].Name == "RunColumn" && !sequenceGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly)
                RunSequence(sequence);
        }

        private void SequenceGrid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= sequenceGrid.Rows.Count)
                return;

            var sequence = sequenceGrid.Rows[e.RowIndex].Tag as SequenceDefinition;
            e.ToolTipText = sequence?.Description ?? string.Empty;
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
            foreach (DataGridViewRow row in sequenceGrid.Rows)
            {
                var sequence = row.Tag as SequenceDefinition;
                if (sequence == null)
                    continue;

                var parsed = SequenceParser.Parse(sequence.Script);
                row.Cells["RunColumn"].ReadOnly = !parsed.IsValid || parsed.Resources.Any(resource => resourceLocks.IsBusy(resource));
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

        private void LoadLayoutSettings()
        {
            if (string.IsNullOrEmpty(layoutSettingsPath))
                return;

            MainLayoutSettings settings;
            try
            {
                settings = MainLayoutSettings.Load(layoutSettingsPath);
            }
            catch
            {
                return;
            }

            if (settings == null)
                return;

            StartPosition = FormStartPosition.Manual;
            if (settings.Width >= MinimumSize.Width && settings.Height >= MinimumSize.Height)
                SetBounds(settings.Left, settings.Top, settings.Width, settings.Height);

            if (settings.MainSplitterDistance > 0 && splitContainer.Width > 0)
            {
                splitContainer.SplitterDistance = Math.Min(settings.MainSplitterDistance, Math.Max(splitContainer.Panel1MinSize, splitContainer.Width - splitContainer.Panel2MinSize));
                defaultSplitterApplied = true;
            }

            ApplyPanePercents(sequencePaneLayout, settings.SequenceListPercent, 42);
            ApplyPanePercents(devicePaneLayout, settings.DeviceListPercent, 62);

            if (settings.WindowState != FormWindowState.Minimized)
                WindowState = settings.WindowState;
        }

        private void SaveLayoutSettings()
        {
            if (string.IsNullOrEmpty(layoutSettingsPath))
                return;

            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var settings = new MainLayoutSettings
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                WindowState = WindowState,
                MainSplitterDistance = splitContainer.SplitterDistance,
                SequenceListPercent = GetPanePercent(sequencePaneLayout, 42),
                DeviceListPercent = GetPanePercent(devicePaneLayout, 62)
            };
            settings.Save(layoutSettingsPath);
        }

        private static void ApplyPanePercents(TableLayoutPanel layout, int primaryPercent, int defaultPercent)
        {
            int percent = primaryPercent > 0 && primaryPercent < 100 ? primaryPercent : defaultPercent;
            layout.RowStyles[1].SizeType = SizeType.Percent;
            layout.RowStyles[1].Height = percent;
            layout.RowStyles[3].SizeType = SizeType.Percent;
            layout.RowStyles[3].Height = 100 - percent;
        }

        private static int GetPanePercent(TableLayoutPanel layout, int defaultPercent)
        {
            float total = layout.RowStyles[1].Height + layout.RowStyles[3].Height;
            if (total <= 0)
                return defaultPercent;

            return (int)Math.Round((layout.RowStyles[1].Height / total) * 100);
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
