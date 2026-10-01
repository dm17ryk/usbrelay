using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace usbrelay
{
    [DataContract]
    public sealed class ThemeSettings
    {
        [DataMember]
        public string Theme { get; set; } = "dark";

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "usbrelay", "theme.json");

        public static ThemeSettings Load(string path)
        {
            if (!File.Exists(path))
                return new ThemeSettings();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var settings = (ThemeSettings)new DataContractJsonSerializer(typeof(ThemeSettings)).ReadObject(stream);
                    return new ThemeSettings { Theme = Normalize(settings.Theme) };
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[ThemeSettings] Load failed; using dark theme: " + ex);
                return new ThemeSettings();
            }
        }

        public static string Normalize(string value)
        {
            if (string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase)) return "dark";
            if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase)) return "light";
            throw new ArgumentException("Theme must be dark or light.");
        }

        public void Save(string path)
        {
            Theme = Normalize(Theme);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (var stream = File.Create(path))
                new DataContractJsonSerializer(typeof(ThemeSettings)).WriteObject(stream, this);
            Trace.WriteLine("[ThemeSettings] Saved theme=" + Theme + ", path=" + path);
        }
    }

    public sealed class GuiTheme
    {
        public GuiTheme(string name) { IsDark = ThemeSettings.Normalize(name) == "dark"; }
        public bool IsDark { get; }
        public Color Background => IsDark ? Color.FromArgb(30, 32, 36) : SystemColors.Control;
        public Color Surface => IsDark ? Color.FromArgb(40, 43, 48) : SystemColors.Window;
        public Color Foreground => IsDark ? Color.FromArgb(230, 232, 235) : SystemColors.ControlText;
        public Color Muted => IsDark ? Color.FromArgb(164, 171, 180) : SystemColors.GrayText;
        public Color Selection => IsDark ? Color.FromArgb(48, 83, 117) : SystemColors.Highlight;
        public Color OnForeground => IsDark ? Color.FromArgb(139, 225, 170) : Color.DarkGreen;
        public Color OnBackground => IsDark ? Color.FromArgb(32, 65, 47) : Color.Honeydew;
        public Color OffForeground => IsDark ? Color.FromArgb(238, 170, 163) : Color.Firebrick;

        public void Apply(Control control)
        {
            control.BackColor = control is TextBoxBase || control is ComboBox ? Surface : Background;
            control.ForeColor = Foreground;
            var button = control as Button;
            if (button != null)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Surface;
                button.FlatAppearance.BorderColor = Muted;
                button.FlatAppearance.MouseOverBackColor = Selection;
            }
            var combo = control as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = FlatStyle.Flat;
                if (combo.DrawMode != DrawMode.OwnerDrawFixed)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += (sender, args) =>
                    {
                        bool selected = (args.State & DrawItemState.Selected) != 0;
                        using (var brush = new SolidBrush(selected ? SystemColors.Highlight : combo.BackColor))
                            args.Graphics.FillRectangle(brush, args.Bounds);
                        if (args.Index >= 0)
                            TextRenderer.DrawText(args.Graphics, combo.GetItemText(combo.Items[args.Index]), combo.Font,
                                args.Bounds, selected ? SystemColors.HighlightText : combo.ForeColor, TextFormatFlags.Left);
                        args.DrawFocusRectangle();
                    };
                }
            }
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = Surface;
                grid.GridColor = Background;
                grid.DefaultCellStyle.BackColor = Surface;
                grid.DefaultCellStyle.ForeColor = Foreground;
                grid.DefaultCellStyle.SelectionBackColor = Selection;
                grid.DefaultCellStyle.SelectionForeColor = IsDark ? Foreground : SystemColors.HighlightText;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Background;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Foreground;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Selection;
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    var buttonColumn = column as DataGridViewButtonColumn;
                    if (buttonColumn != null) buttonColumn.FlatStyle = FlatStyle.Flat;
                }
            }
            foreach (Control child in control.Controls) Apply(child);
            control.Invalidate();
        }

        public void ApplyTitleBar(Form form)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !form.IsHandleCreated) return;
            int value = IsDark ? 1 : 0;
            try
            {
                int result = DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int));
                Trace.WriteLine("[GuiTheme] Title bar dark=" + IsDark + ", result=" + result);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Trace.WriteLine("[GuiTheme] Native title bar theming unavailable: " + ex.Message);
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }
}
