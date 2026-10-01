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
        private GuiTheme theme;
        private readonly string themeSettingsPath;
        private ToolStripMenuItem darkThemeMenuItem;
        private ToolStripMenuItem lightThemeMenuItem;
        private ToolStripMenuItem editSequenceMenuItem;
        private ToolStripMenuItem removeSequenceMenuItem;
        private ToolStripMenuItem editNamesMenuItem;
        private ToolStripMenuItem allOffMenuItem;
        private Label sequencesHeading;
        private Label devicesHeading;
        private readonly RelayService relayService;
        private readonly RelayNamingRepository relayNamingRepository;
        private readonly SequenceRepository sequenceRepository;
        private readonly string layoutSettingsPath;
        private readonly Func<SequenceDefinition, bool> removeSequenceConfirmation;
        private readonly Func<string, string, bool> sequenceConfirmation;
        private readonly SequenceResourceLocks resourceLocks = new SequenceResourceLocks();
        private readonly SequenceParseCache sequenceParseCache = new SequenceParseCache();
        private readonly List<SequenceDefinition> sequences = new List<SequenceDefinition>();
        private IReadOnlyList<RelayDevice> currentDevices = new RelayDevice[0];

        private DataGridView sequenceGrid;
        private FlowLayoutPanel devicesPanel;
        private SplitContainer splitContainer;
        private TableLayoutPanel sequencePaneLayout;
        private TableLayoutPanel devicePaneLayout;
        private TextBox logTextBox;
        private DataGridView statusGrid;
        private SequenceDefinition selectedSequence;
        private bool loaded;
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
            : this(relayBackend, sequenceRepository, layoutSettingsPath, null, null)
        {
        }

        public MainForm(
            IRelayBackend relayBackend,
            SequenceRepository sequenceRepository,
            string layoutSettingsPath,
            Func<SequenceDefinition, bool> removeSequenceConfirmation)
            : this(relayBackend, sequenceRepository, layoutSettingsPath, removeSequenceConfirmation, null)
        {
        }

        public MainForm(
            IRelayBackend relayBackend,
            SequenceRepository sequenceRepository,
            string layoutSettingsPath,
            Func<SequenceDefinition, bool> removeSequenceConfirmation,
            Func<string, string, bool> sequenceConfirmation,
            string themeSettingsPath = null)
        {
            this.themeSettingsPath = themeSettingsPath ?? ThemeSettings.DefaultPath;
            theme = new GuiTheme(ThemeSettings.Load(this.themeSettingsPath).Theme);
            this.relayNamingRepository = new RelayNamingRepository(RelayNamingRepository.DefaultPath);
            this.relayService = new RelayService(relayBackend, relayNamingRepository);
            this.sequenceRepository = sequenceRepository;
            this.layoutSettingsPath = layoutSettingsPath;
            this.removeSequenceConfirmation = removeSequenceConfirmation ?? ConfirmRemoveSequence;
            this.sequenceConfirmation = sequenceConfirmation ?? ConfirmSequence;
            InitializeComponent();
            theme.Apply(this);
            HandleCreated += (s, e) => theme.ApplyTitleBar(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PrepareForDisplay();
        }

        public void PrepareForDisplay()
        {
            if (loaded)
                return;

            LoadLayoutSettings();
            LoadSequences();
            RefreshDevices();
            loaded = true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyDefaultSplitterDistance();
            ResizeDeviceRows();
            UpdateBusyState();
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
            Font = new Font("Segoe UI", 9.5F);
            Icon icon = AppAssets.LoadApplicationIcon();
            if (icon != null)
                Icon = icon;

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 6,
                FixedPanel = FixedPanel.None
            };

            splitContainer.Panel1.Controls.Add(CreateSequencePane());
            splitContainer.Panel2.Controls.Add(CreateDevicePane());
            Controls.Add(splitContainer);
            MainMenuStrip = CreateMenu();
            Controls.Add(MainMenuStrip);
            Resize += (s, e) => ResizeDeviceRows();
        }

        private MenuStrip CreateMenu()
        {
            var menu = new MenuStrip { Name = "mainMenu", Dock = DockStyle.Top, Padding = new Padding(8, 4, 8, 4) };
            var sequencesMenu = new ToolStripMenuItem("&Sequences");
            sequencesMenu.DropDownItems.Add(MenuAction("&Add sequence...", "addSequenceMenuItem", AddSequence, Keys.Control | Keys.N));
            editSequenceMenuItem = MenuAction("&Edit sequence...", "editSequenceMenuItem", EditSequence, Keys.Control | Keys.E);
            removeSequenceMenuItem = MenuAction("&Remove sequence...", "removeSequenceMenuItem", RemoveSequence);
            sequencesMenu.DropDownItems.Add(editSequenceMenuItem);
            sequencesMenu.DropDownItems.Add(removeSequenceMenuItem);
            sequencesMenu.DropDownOpening += (s, e) => UpdateSequenceMenuState();

            var devicesMenu = new ToolStripMenuItem("&Devices");
            devicesMenu.DropDownItems.Add(MenuAction("&Refresh devices", "refreshDevicesMenuItem", RefreshDevices, Keys.F5));
            editNamesMenuItem = MenuAction("Edit &names...", "editNamesMenuItem", EditDeviceNames);
            allOffMenuItem = MenuAction("All &off", "allOffMenuItem", AllOff);
            devicesMenu.DropDownItems.Add(editNamesMenuItem);
            devicesMenu.DropDownItems.Add(new ToolStripSeparator());
            devicesMenu.DropDownItems.Add(allOffMenuItem);

            var viewMenu = new ToolStripMenuItem("&View");
            var themeMenu = new ToolStripMenuItem("&Theme") { Name = "themeMenuItem" };
            darkThemeMenuItem = MenuAction("&Dark", "darkThemeMenuItem", () => ChangeTheme("dark"));
            lightThemeMenuItem = MenuAction("&Light", "lightThemeMenuItem", () => ChangeTheme("light"));
            themeMenu.DropDownItems.AddRange(new ToolStripItem[] { darkThemeMenuItem, lightThemeMenuItem });
            viewMenu.DropDownItems.Add(themeMenu);
            UpdateThemeMenuState();

            var toolsMenu = new ToolStripMenuItem("&Tools");
            toolsMenu.DropDownItems.Add(MenuAction("&AI tools and MCP setup...", "aiToolsMenuItem", () =>
            {
                using (var form = new IntegrationForm(theme)) form.ShowDialog(this);
            }));
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(MenuAction("&Quick start...", "helpMenuItem", () =>
            {
                using (var form = new HelpForm(theme)) form.ShowDialog(this);
            }, Keys.F1));
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add(MenuAction("&About USB Relay Control...", "aboutMenuItem", () =>
            {
                using (var form = new AboutForm(theme)) form.ShowDialog(this);
            }));
            menu.Items.AddRange(new ToolStripItem[] { sequencesMenu, devicesMenu, viewMenu, toolsMenu, helpMenu });
            UpdateSequenceMenuState();
            System.Diagnostics.Trace.WriteLine("[MainForm] Menu created: sequences, devices, view/theme, tools/MCP, help/about");
            return menu;
        }

        private ToolStripMenuItem MenuAction(string text, string name, Action action, Keys shortcut = Keys.None)
        {
            var item = new ToolStripMenuItem(text) { Name = name, ShortcutKeys = shortcut, AccessibleName = text.Replace("&", "") };
            item.Click += (s, e) =>
            {
                System.Diagnostics.Trace.WriteLine("[MainForm] Menu action=" + name);
                action();
            };
            return item;
        }

        private void UpdateSequenceMenuState()
        {
            bool selected = selectedSequence != null;
            if (editSequenceMenuItem != null) editSequenceMenuItem.Enabled = selected;
            if (removeSequenceMenuItem != null) removeSequenceMenuItem.Enabled = selected;
            System.Diagnostics.Trace.WriteLine("[MainForm] Sequence menu selection=" + (selectedSequence?.Name ?? "none") + ", edit/remove enabled=" + selected);
        }

        private void UpdateThemeMenuState()
        {
            darkThemeMenuItem.Checked = theme.IsDark;
            lightThemeMenuItem.Checked = !theme.IsDark;
        }

        private Control CreateSequencePane()
        {
            sequencePaneLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12, 8, 6, 12)
            };
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sequencePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));

            sequencesHeading = new Label { Text = "Sequences", AutoSize = true, Margin = new Padding(0, 6, 0, 10) };
            sequencePaneLayout.Controls.Add(sequencesHeading, 0, 0);

            sequenceGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                ColumnHeadersVisible = false,
                MultiSelect = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Vertical,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                AccessibleName = "Saved sequences"
            };
            sequenceGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NameColumn", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            sequenceGrid.Columns.Add(new DataGridViewButtonColumn { Name = "RunColumn", Width = 106, MinimumWidth = 64, UseColumnTextForButtonValue = false });
            sequenceGrid.CellClick += SequenceGrid_CellClick;
            sequenceGrid.CellPainting += PaintSequenceRunButton;
            sequenceGrid.CellMouseEnter += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 1) sequenceGrid.InvalidateCell(e.ColumnIndex, e.RowIndex); };
            sequenceGrid.CellMouseLeave += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 1) sequenceGrid.InvalidateCell(e.ColumnIndex, e.RowIndex); };
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
                BorderStyle = BorderStyle.None,
                AccessibleName = "Sequence log",
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
                Padding = new Padding(6, 8, 12, 12)
            };
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            devicePaneLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            devicesHeading = new Label { Text = "Devices", AutoSize = true, Margin = new Padding(0, 6, 0, 10) };
            devicePaneLayout.Controls.Add(devicesHeading, 0, 0);

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
            statusGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Both,
                AccessibleName = "Device and channel status"
            };
            statusGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusName", HeaderText = "Name", Width = 110 });
            statusGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusSerial", HeaderText = "Serial", Width = 82 });
            statusGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusType", HeaderText = "Type", Width = 105 });
            statusGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusPath", HeaderText = "Device Path", Width = 330, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            for (int channel = 1; channel <= 8; channel++)
            {
                var column = new DataGridViewTextBoxColumn
                {
                    Name = "StatusChannel" + channel,
                    HeaderText = "CH" + channel,
                    Width = 78,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        WrapMode = DataGridViewTriState.True
                    }
                };
                statusGrid.Columns.Add(column);
            }
            statusGrid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 4 || !(e.Value is string))
                    return;

                string value = (string)e.Value;
                if (value.EndsWith("ON", StringComparison.Ordinal))
                {
                    e.CellStyle.ForeColor = theme.OnForeground;
                    e.CellStyle.BackColor = theme.OnBackground;
                }
                else if (value.EndsWith("OFF", StringComparison.Ordinal))
                {
                    e.CellStyle.ForeColor = theme.Muted;
                    e.CellStyle.BackColor = theme.Surface;
                }
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
                e.CellStyle.SelectionBackColor = theme.Selection;
            };
            devicePaneLayout.Controls.Add(statusGrid, 0, 3);

            return devicePaneLayout;
        }

        private void ChangeTheme(string name)
        {
            name = ThemeSettings.Normalize(name);
            if (theme.IsDark == (name == "dark"))
            {
                System.Diagnostics.Trace.WriteLine("[MainForm] Theme unchanged=" + name);
                return;
            }
            System.Diagnostics.Trace.WriteLine("[MainForm] Theme selected=" + name);
            try
            {
                new ThemeSettings { Theme = name }.Save(themeSettingsPath);
            }
            catch (Exception ex)
            {
                AppendLog("Theme preference could not be saved: " + ex.Message);
            }
            theme = new GuiTheme(name);
            theme.Apply(this);
            theme.ApplyTitleBar(this);
            UpdateThemeMenuState();
            RenderDeviceCards();
            UpdateBusyState();
            AppendLog("Theme changed to " + name);
        }

        private void ApplyDefaultSplitterDistance()
        {
            if (defaultSplitterApplied || splitContainer.Width <= 0)
                return;

            int maximum = Math.Max(splitContainer.Panel1MinSize, splitContainer.Width - splitContainer.Panel2MinSize - splitContainer.SplitterWidth);
            splitContainer.SplitterDistance = Math.Min(maximum, Math.Max(splitContainer.Panel1MinSize, (int)(splitContainer.Width * 0.4)));
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
            SequenceDefinition preferredSelection = selectedSequence;
            sequenceGrid.Rows.Clear();
            sequenceParseCache.Retain(sequences);

            foreach (var sequence in sequences)
            {
                int rowIndex = sequenceGrid.Rows.Add(sequence.Name, sequence.DisplayRunButtonText);
                var row = sequenceGrid.Rows[rowIndex];
                row.Tag = sequence;
                row.Height = 30;
                foreach (DataGridViewCell cell in row.Cells)
                    cell.ToolTipText = sequence.Description ?? string.Empty;
                if (ReferenceEquals(sequence, preferredSelection))
                    row.Selected = true;
            }

            sequencesHeading.Text = "Sequences · " + sequences.Count;
            SelectGridSequence();
            UpdateBusyState();
        }

        private void SelectGridSequence()
        {
            if (sequenceGrid.SelectedRows.Count == 0)
            {
                selectedSequence = null;
                UpdateSequenceMenuState();
                return;
            }

            selectedSequence = sequenceGrid.SelectedRows[0].Tag as SequenceDefinition;
            UpdateSequenceMenuState();
        }

        private void SequenceGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= sequenceGrid.Rows.Count)
                return;

            var sequence = sequenceGrid.Rows[e.RowIndex].Tag as SequenceDefinition;
            if (sequence == null)
                return;

            selectedSequence = sequence;
            if (sequenceGrid.Columns[e.ColumnIndex].Name == "RunColumn")
            {
                var parsed = sequenceParseCache.Get(sequence);
                if (!parsed.IsValid || parsed.Resources.Any(resource => resourceLocks.IsBusy(resource)))
                    return;

                RunSequence(sequence);
            }
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
            using (var form = new SequenceEditorForm(null, currentDevices))
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
            using (var form = new SequenceEditorForm(selectedSequence, currentDevices))
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

            if (!removeSequenceConfirmation(selectedSequence))
                return;

            sequences.Remove(selectedSequence);
            selectedSequence = null;
            SaveSequences();
            RenderSequences();
        }

        private bool ConfirmRemoveSequence(SequenceDefinition sequence)
        {
            string name = sequence == null || string.IsNullOrWhiteSpace(sequence.Name)
                ? "this sequence"
                : "\"" + sequence.Name + "\"";

            return MessageBox.Show(
                this,
                "Remove " + name + "?",
                "Remove sequence",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private async void RunSequence(SequenceDefinition sequence)
        {
            var parsed = sequenceParseCache.Get(sequence);
            if (!parsed.IsValid)
            {
                AppendLog(sequence.Name + " validation failed: " + string.Join("; ", parsed.Diagnostics));
                return;
            }

            string owner = Guid.NewGuid().ToString("N");
            RelayResource[] resources = ResolveResources(parsed.Resources).ToArray();
            if (!resourceLocks.TryReserve(owner, resources))
            {
                AppendLog(sequence.Name + " waiting: required channel is busy");
                return;
            }

            UpdateBusyState();
            AppendLog(sequence.Name + " started");
            AppendLog("reserved " + string.Join(", ", resources.Select(DescribeResource)));

            try
            {
                var result = await Task.Run(() => SequenceRunner.Run(
                    parsed,
                    new RelaySequenceBackend(relayService),
                    new ProcessExternalToolRunner(),
                    skipDelays: false,
                    actionCompleted: RefreshDeviceStatusFromSequence,
                    confirmation: sequenceConfirmation));
                foreach (string line in result.Log)
                    AppendLog(line);
                AppendLog(sequence.Name + " " + result.StatusText);
            }
            finally
            {
                resourceLocks.Release(owner);
                if (!IsDisposed && !Disposing)
                {
                    AppendLog("released " + sequence.Name);
                    RefreshDevices();
                    UpdateBusyState();
                }
            }
        }

        private bool ConfirmSequence(string title, string message)
        {
            if (IsDisposed || Disposing)
                return false;

            if (InvokeRequired)
                return (bool)Invoke((Func<bool>)(() => ConfirmSequence(title, message)));

            return MessageBox.Show(
                this,
                message ?? string.Empty,
                title ?? string.Empty,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.OK;
        }

        private void RefreshDevices()
        {
            IReadOnlyList<RelayDevice> devices;
            try
            {
                devices = relayService.EnumerateDevices();
                currentDevices = devices.ToArray();
            }
            catch (Exception ex)
            {
                AppendLog("Refresh failed: " + ex.Message);
                return;
            }

            RenderDeviceCards();
            statusGrid.Rows.Clear();

            foreach (var device in devices)
            {
                int rowIndex = statusGrid.Rows.Add(new object[12]);
                UpdateStatusRow(statusGrid.Rows[rowIndex], device);
            }

            if (devices.Count == 0)
            {
                int rowIndex = statusGrid.Rows.Add("No USB relay devices discovered.");
                statusGrid.Rows[rowIndex].Cells[0].ToolTipText = "Connect a USB relay device, then use Devices > Refresh devices (F5).";
            }

            ResizeDeviceRows();
            UpdateBusyState();
        }

        private void RenderDeviceCards()
        {
            devicesPanel.SuspendLayout();
            try
            {
                while (devicesPanel.Controls.Count > 0) devicesPanel.Controls[0].Dispose();
                foreach (var device in currentDevices) devicesPanel.Controls.Add(CreateDevicePanel(device));
                devicesHeading.Text = "Devices · " + currentDevices.Count + " connected";
                if (editNamesMenuItem != null) editNamesMenuItem.Enabled = currentDevices.Count > 0;
                if (allOffMenuItem != null) allOffMenuItem.Enabled = currentDevices.Count > 0;
                System.Diagnostics.Trace.WriteLine("[MainForm] Render device cards: count=" + currentDevices.Count + ", dark=" + theme.IsDark);
            }
            finally { devicesPanel.ResumeLayout(true); }
            ResizeDeviceRows();
        }

        private void PaintSequenceRunButton(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
            e.PaintBackground(e.ClipBounds, true);
            var cell = sequenceGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var bounds = Rectangle.Inflate(e.CellBounds, -5, -4);
            if (bounds.Width < 5 || bounds.Height < 5) { e.Handled = true; return; }
            bool hovered = bounds.Contains(sequenceGrid.PointToClient(Cursor.Position));
            using (var brush = new SolidBrush(hovered && !cell.ReadOnly ? theme.Hover : theme.Surface))
            using (var pen = new Pen(theme.Border))
            {
                e.Graphics.FillRectangle(brush, bounds);
                e.Graphics.DrawRectangle(pen, bounds);
            }
            TextRenderer.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), e.CellStyle.Font, bounds,
                cell.ReadOnly ? theme.Muted : theme.Foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (ReferenceEquals(sequenceGrid.CurrentCell, cell) && sequenceGrid.ContainsFocus)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -2, -2), theme.Foreground, theme.Surface);
            e.Handled = true;
        }

        private void RefreshDeviceStatusTable()
        {
            IReadOnlyList<RelayDevice> devices;
            try
            {
                devices = relayService.EnumerateDevices();
                currentDevices = devices.ToArray();
            }
            catch (Exception ex)
            {
                AppendLog("Status refresh failed: " + ex.Message);
                return;
            }

            if (devices.Count == 0)
            {
                if (statusGrid.Rows.Count != 1)
                    RefreshDevices();
                return;
            }

            if (statusGrid.Rows.Count != devices.Count)
            {
                RefreshDevices();
                return;
            }

            for (int index = 0; index < devices.Count; index++)
                UpdateStatusRow(statusGrid.Rows[index], devices[index]);

            UpdateBusyState();
        }

        private void RefreshDeviceStatusFromSequence()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(RefreshDeviceStatusTable));
            }
            catch (InvalidOperationException)
            {
                // The form can close while a sequence worker reports its last action.
            }
        }

        private void UpdateStatusRow(DataGridViewRow statusRow, RelayDevice device)
        {
            statusRow.Cells[0].Value = device.DisplayName;
            statusRow.Cells[1].Value = device.SerialNumber;
            statusRow.Cells[2].Value = device.Type;
            statusRow.Cells[3].Value = device.DevicePath;
            statusRow.Cells[3].ToolTipText = device.DevicePath;
            for (int channel = 1; channel <= device.ChannelCount; channel++)
                statusRow.Cells[channel + 3].Value = device.GetChannelName(channel) + Environment.NewLine + (device.IsChannelOn(channel) ? "ON" : "OFF");
            statusRow.Height = 38;
        }

        private Control CreateDevicePanel(RelayDevice device)
        {
            var group = new RelayCard
            {
                Text = device.DisplayName + " · " + device.SerialNumber + " · " + device.Type,
                Width = DeviceRowWidth(),
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(10, 32, 10, 10),
                Tag = device.ChannelCount,
                AccessibleName = "USB relay " + device.SerialNumber + "; device path " + device.DevicePath
            };

            var channels = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true,
                Padding = new Padding(0)
            };

            for (int channel = 1; channel <= device.ChannelCount; channel++)
            {
                int capturedChannel = channel;
                bool on = device.IsChannelOn(channel);
                var button = new RelayChannelButton(theme, device.GetChannelName(channel), on)
                {
                    ForeColor = on ? theme.OnForeground : theme.OffForeground,
                    Width = 70,
                    Height = 64,
                    Margin = new Padding(0, 0, 6, 6),
                    AccessibleName = device.DisplayName + ", " + device.GetChannelName(channel) + ", " + (on ? "ON" : "OFF"),
                    Tag = new RelayResource(device.SerialNumber, channel, device.DevicePath)
                };
                button.Click += (s, e) => ToggleChannel(device, capturedChannel, !on);
                channels.Controls.Add(button);
            }

            group.Controls.Add(channels);
            theme.Apply(group);
            for (int index = 0; index < channels.Controls.Count; index++)
            {
                channels.Controls[index].ForeColor = device.IsChannelOn(index + 1) ? theme.OnForeground : theme.Muted;
                channels.Controls[index].BackColor = device.IsChannelOn(index + 1) ? theme.OnBackground : theme.Surface;
            }
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
            int usableWidth = Math.Max(70, row.Width - 20);
            int cellWidth = Math.Max(64, Math.Min(76, usableWidth / Math.Max(1, channelCount)));
            int perRow = Math.Max(1, usableWidth / cellWidth);
            int rows = Math.Max(1, (int)Math.Ceiling(channelCount / (double)perRow));
            foreach (Control panel in row.Controls)
                foreach (Control button in panel.Controls)
                    if (button.Tag is RelayResource) button.Width = cellWidth - 6;
            row.Height = 42 + (rows * 70);
        }

        private int DeviceRowWidth()
        {
            return Math.Max(260, devicesPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        }

        private void ToggleChannel(RelayDevice device, int channel, bool on, bool refreshDevices = true)
        {
            var resource = new RelayResource(device.SerialNumber, channel, device.DevicePath);
            if (resourceLocks.IsBusy(resource))
            {
                System.Diagnostics.Trace.WriteLine(
                    "[MainForm] ToggleChannel: skipped busy device serial=" + device.SerialNumber
                    + ", path=" + device.DevicePath
                    + ", channel=" + channel);
                return;
            }

            try
            {
                System.Diagnostics.Trace.WriteLine(
                    "[MainForm] ToggleChannel: routing device serial=" + device.SerialNumber
                    + ", path=" + device.DevicePath
                    + ", channel=" + channel
                    + ", on=" + on);
                relayService.SetChannel(device, channel, on);
                AppendLog(DescribeChannel(device, channel) + " -> " + (on ? "ON" : "OFF") + " ok");
                if (refreshDevices)
                    RefreshDevices();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    "[MainForm] ToggleChannel: failed device serial=" + device.SerialNumber
                    + ", path=" + device.DevicePath
                    + ", channel=" + channel
                    + ", error=" + ex);
                AppendLog(DescribeChannel(device, channel) + " failed: " + ex.Message);
            }
        }

        private IEnumerable<RelayResource> ResolveResources(IEnumerable<RelayResource> resources)
        {
            var resolved = new List<RelayResource>();
            foreach (RelayResource resource in resources)
            {
                RelayDevice device = currentDevices.FirstOrDefault(item => item.MatchesSelector(resource.SerialNumber));
                if (device == null)
                {
                    resolved.Add(resource);
                    continue;
                }

                int channel;
                try
                {
                    channel = string.IsNullOrEmpty(resource.ChannelName)
                        ? resource.Channel
                        : device.ResolveChannel(resource.ChannelName);
                }
                catch (Exception)
                {
                    resolved.Add(resource);
                    continue;
                }

                resolved.Add(new RelayResource(device.SerialNumber, channel, device.DevicePath));
            }

            return resolved;
        }

        private void EditDeviceNames()
        {
            using (var form = new DeviceNamingForm(currentDevices, relayNamingRepository))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    RefreshDevices();
            }
        }

        private void AllOff()
        {
            foreach (var device in relayService.EnumerateDevices())
            {
                for (int channel = 1; channel <= device.ChannelCount; channel++)
                    ToggleChannel(device, channel, false, refreshDevices: false);
            }

            RefreshDevices();
        }

        private void UpdateBusyState()
        {
            foreach (DataGridViewRow row in sequenceGrid.Rows)
            {
                var sequence = row.Tag as SequenceDefinition;
                if (sequence == null)
                    continue;

                var parsed = sequenceParseCache.Get(sequence);
                bool busy = ResolveResources(parsed.Resources).Any(resource => resourceLocks.IsBusy(resource));
                var runCell = row.Cells["RunColumn"];
                runCell.Value = !parsed.IsValid ? "Invalid" : busy ? "Busy" : sequence.DisplayRunButtonText;
                runCell.ReadOnly = !parsed.IsValid || busy;
                runCell.Style.ForeColor = !parsed.IsValid || busy ? theme.Muted : theme.Foreground;
                runCell.Style.BackColor = !parsed.IsValid || busy ? theme.Background : theme.Surface;
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
            if (IsDisposed || Disposing || logTextBox == null || logTextBox.IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                }
                catch (InvalidOperationException)
                {
                    // The form can close between the lifecycle check and BeginInvoke.
                }
                return;
            }

            if (IsDisposed || Disposing || logTextBox.IsDisposed)
                return;

            logTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }

        private string DescribeResource(RelayResource resource)
        {
            RelayDevice device = currentDevices.FirstOrDefault(item => item.MatchesSelector(resource.SerialNumber));
            if (device == null)
                return resource.ToString();

            try
            {
                int channel = string.IsNullOrEmpty(resource.ChannelName)
                    ? resource.Channel
                    : device.ResolveChannel(resource.ChannelName);
                return DescribeChannel(device, channel);
            }
            catch (Exception)
            {
                return resource.ToString();
            }
        }

        private static string DescribeChannel(RelayDevice device, int channel)
        {
            return device.DisplayName + " " + device.GetChannelName(channel) + " (CH" + channel + ")";
        }
    }
}
