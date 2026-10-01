using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace usbrelay
{
    public sealed class AboutForm : Form
    {
        public const string ProjectUrl = "https://github.com/dm17ryk/usbrelay";
        public static string ApplicationVersion => typeof(AboutForm).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? typeof(AboutForm).Assembly.GetName().Version.ToString();

        public AboutForm(GuiTheme theme)
        {
            Text = "About USB Relay Control";
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(480, 310);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            Icon = AppAssets.LoadApplicationIcon();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(24) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(new Label { Text = "USB Relay Control", Font = new Font(Font.FontFamily, 18F), AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Name = "versionLabel", Text = "Version " + ApplicationVersion, AutoSize = true }, 0, 1);
            layout.Controls.Add(new Label { Text = "Control USB relays and run repeatable sequences.\r\nGUI · command line · MCP server", AutoSize = true }, 0, 2);
            layout.Controls.Add(new Label { Text = "Based on usb-relay-hid by pavel-a.", AutoSize = true }, 0, 3);
            var link = new LinkLabel { Text = ProjectUrl, AutoSize = true, AccessibleName = "USB Relay Control project on GitHub" };
            link.LinkClicked += (s, e) =>
            {
                Trace.WriteLine("[AboutForm] Open project documentation: " + ProjectUrl);
                try { Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    Trace.WriteLine("[AboutForm] Could not open project documentation: " + ex);
                    MessageBox.Show(this, "Could not open your browser. Copy this address:\r\n" + ProjectUrl, "Project documentation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            layout.Controls.Add(link, 0, 4);
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Size = new Size(90, 30), Anchor = AnchorStyles.Right };
            layout.Controls.Add(close, 0, 5);
            Controls.Add(layout);
            AcceptButton = close;
            CancelButton = close;
            theme.Apply(this);
            HandleCreated += (s, e) => theme.ApplyTitleBar(this);
            Trace.WriteLine("[AboutForm] Initialized version=" + ApplicationVersion + ", dark=" + theme.IsDark);
        }
    }

    public sealed class HelpForm : Form
    {
        public HelpForm(GuiTheme theme)
        {
            Text = "USB Relay Control · Quick start";
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(660, 510);
            MinimumSize = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            Icon = AppAssets.LoadApplicationIcon();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.Controls.Add(new Label { Text = "Quick start", Font = new Font(Font.FontFamily, 18F), AutoSize = true }, 0, 0);
            var help = new RichTextBox { Name = "helpText", Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false };
            help.Text = "DEVICES\r\nConnect your USB relay board, then use Devices > Refresh devices (F5). Click a channel to switch it; ON and OFF labels show its current state. Devices > All off switches available channels off. Channels reserved by a running sequence remain busy.\r\n\r\n"
                + "NAMES\r\nUse Devices > Edit names to name boards and channels. Names are saved by the unique device path, so boards with the same serial number stay separate.\r\n\r\n"
                + "SEQUENCES\r\nUse Sequences > Add sequence (Ctrl+N) to create a script. Select a row, then use Edit sequence (Ctrl+E) or Remove sequence. Click a row's Run button to execute it; results appear in Sequence log. Invalid or busy sequences cannot run.\r\n\r\n"
                + "APPEARANCE AND AI TOOLS\r\nChoose View > Theme > Dark or Light. Use Tools > AI tools and MCP setup to preview or install MCP registration and skills for Codex, Claude Code, Cursor, and VS Code.\r\n\r\n"
                + "COMMAND LINE\r\nusbrelay --help\r\nusbrelay --list\r\nusbrelay --status\r\nusbrelay sequence functions\r\nusbrelay integration install --dry-run\r\n\r\nPress F1 in the main window to open this guide. Help > About shows the version and project link.";
            layout.Controls.Add(help, 0, 1);
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Size = new Size(90, 30), Anchor = AnchorStyles.Right };
            layout.Controls.Add(close, 0, 2);
            Controls.Add(layout);
            AcceptButton = close;
            CancelButton = close;
            theme.Apply(this);
            HandleCreated += (s, e) => theme.ApplyTitleBar(this);
            Trace.WriteLine("[HelpForm] Offline guide initialized, dark=" + theme.IsDark);
        }
    }
}
