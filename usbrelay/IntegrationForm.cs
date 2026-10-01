using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace usbrelay
{
    internal sealed class IntegrationForm : Form
    {
        private readonly CheckedListBox clients = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, AccessibleName = "AI clients" };
        private readonly CheckBox includeSkill = new CheckBox { Text = "Create USB Relay skill", Checked = true, AutoSize = true };
        private readonly TextBox output = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F), AccessibleName = "Integration setup log" };

        public IntegrationForm(GuiTheme theme)
        {
            Text = "AI tool integrations";
            Width = 760;
            Height = 520;
            MinimumSize = new Size(600, 420);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "Register USB Relay's MCP server for this Windows user. Restart the client after setup.", AutoSize = true, Margin = new Padding(0, 0, 0, 8) }, 0, 0);
            clients.Items.AddRange(IntegrationInstaller.SupportedClients);
            layout.Controls.Add(clients, 0, 1);
            var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            controls.Controls.Add(includeSkill);
            var preview = new Button { Text = "Preview", AutoSize = true };
            var install = new Button { Text = "Install", AutoSize = true };
            preview.Click += (s, e) => Run(true);
            install.Click += (s, e) => Run(false);
            controls.Controls.Add(preview);
            controls.Controls.Add(install);
            layout.Controls.Add(controls, 0, 2);
            layout.Controls.Add(output, 0, 3);
            Controls.Add(layout);
            theme.Apply(this);
            HandleCreated += (s, e) => theme.ApplyTitleBar(this);
        }

        private void Run(bool preview)
        {
            output.Clear();
            try
            {
                var selected = clients.CheckedItems.Cast<string>().ToArray();
                if (selected.Length == 0) { Append("Select at least one client."); return; }
                var installer = new IntegrationInstaller(Assembly.GetExecutingAssembly().Location, Append);
                installer.Apply(installer.Plan(new IntegrationOptions { Clients = selected, IncludeSkill = includeSkill.Checked }), preview);
            }
            catch (Exception ex) { Append("[integration] Setup failed: " + ex); }
        }

        private void Append(string message) => output.AppendText(message + Environment.NewLine);
    }
}
