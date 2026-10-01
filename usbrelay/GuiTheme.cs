using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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
        public Color Background => IsDark ? Color.FromArgb(24, 28, 35) : Color.FromArgb(242, 245, 249);
        public Color Surface => IsDark ? Color.FromArgb(32, 38, 47) : Color.White;
        public Color Foreground => IsDark ? Color.FromArgb(229, 235, 244) : Color.FromArgb(32, 44, 61);
        public Color Muted => IsDark ? Color.FromArgb(160, 174, 193) : Color.FromArgb(89, 104, 123);
        public Color Border => IsDark ? Color.FromArgb(55, 65, 80) : Color.FromArgb(216, 224, 234);
        public Color Hover => IsDark ? Color.FromArgb(44, 54, 68) : Color.FromArgb(233, 239, 248);
        public Color Selection => IsDark ? Color.FromArgb(43, 67, 94) : Color.FromArgb(215, 232, 253);
        public Color Accent => IsDark ? Color.FromArgb(141, 190, 255) : Color.FromArgb(35, 99, 186);
        public Color OnForeground => IsDark ? Color.FromArgb(145, 226, 182) : Color.FromArgb(24, 111, 67);
        public Color OnBackground => IsDark ? Color.FromArgb(29, 59, 48) : Color.FromArgb(228, 245, 235);
        public Color OffForeground => IsDark ? Color.FromArgb(238, 170, 163) : Color.Firebrick;
        private static readonly ConditionalWeakTable<Control, NativeThemeBinding> nativeThemes = new ConditionalWeakTable<Control, NativeThemeBinding>();

        public void Apply(Control control)
        {
            bool cardSurface = control is RelayCard || control.Parent is RelayCard || control.Parent?.Parent is RelayCard;
            control.BackColor = control is TextBoxBase || control is ComboBox || cardSurface ? Surface : Background;
            control.ForeColor = Foreground;
            var card = control as RelayCard;
            if (card != null) card.Theme = this;
            var channelButton = control as RelayChannelButton;
            if (channelButton != null) channelButton.Theme = this;
            var button = control as Button;
            if (button != null)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Surface;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Hover;
                button.FlatAppearance.MouseDownBackColor = Selection;
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
                        NativeThemeBinding binding;
                        Color selection = nativeThemes.TryGetValue(combo, out binding) ? binding.Selection : SystemColors.Highlight;
                        using (var brush = new SolidBrush(selected ? selection : combo.BackColor))
                            args.Graphics.FillRectangle(brush, args.Bounds);
                        if (args.Index >= 0)
                            TextRenderer.DrawText(args.Graphics, combo.GetItemText(combo.Items[args.Index]), combo.Font,
                                args.Bounds, combo.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        args.DrawFocusRectangle();
                    };
                }
            }
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = Surface;
                grid.GridColor = Border;
                grid.DefaultCellStyle.BackColor = Surface;
                grid.DefaultCellStyle.ForeColor = Foreground;
                grid.DefaultCellStyle.SelectionBackColor = Selection;
                grid.DefaultCellStyle.SelectionForeColor = Foreground;
                grid.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3);
                grid.ColumnHeadersDefaultCellStyle.BackColor = Hover;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Foreground;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Hover;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Foreground;
                grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 5, 6, 5);
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    var buttonColumn = column as DataGridViewButtonColumn;
                    if (buttonColumn != null) buttonColumn.FlatStyle = FlatStyle.Flat;
                }
            }
            var strip = control as ToolStrip;
            if (strip != null)
            {
                var renderer = new GuiMenuRenderer(this);
                strip.Renderer = renderer;
                ApplyMenuItems(strip.Items, renderer);
            }
            var link = control as LinkLabel;
            if (link != null)
            {
                link.LinkColor = Accent;
                link.ActiveLinkColor = Accent;
                link.VisitedLinkColor = Accent;
            }
            if (control is TextBoxBase || control is ComboBox || control is ScrollBar || control is ListView)
                nativeThemes.GetValue(control, c => new NativeThemeBinding(c)).Apply(this);
            foreach (Control child in control.Controls) Apply(child);
            control.Invalidate();
        }

        private void ApplyMenuItems(ToolStripItemCollection items, ToolStripRenderer renderer)
        {
            foreach (ToolStripItem item in items)
            {
                item.ForeColor = item.Enabled ? Foreground : Muted;
                item.BackColor = Surface;
                var menu = item as ToolStripMenuItem;
                if (menu == null || !menu.HasDropDownItems) continue;
                menu.DropDown.BackColor = Surface;
                menu.DropDown.ForeColor = Foreground;
                menu.DropDown.Renderer = renderer;
                ApplyMenuItems(menu.DropDownItems, renderer);
            }
        }

        private sealed class NativeThemeBinding
        {
            private readonly Control control;
            private GuiTheme theme;
            public Color Selection => theme.Selection;

            public NativeThemeBinding(Control control)
            {
                this.control = control;
                control.HandleCreated += (s, e) => ApplyNativeTheme();
            }

            public void Apply(GuiTheme theme)
            {
                this.theme = theme;
                if (control.IsHandleCreated) ApplyNativeTheme();
            }

            private void ApplyNativeTheme()
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
                try
                {
                    int result = SetWindowTheme(control.Handle, theme.IsDark ? "DarkMode_Explorer" : null, null);
                    Trace.WriteLine("[GuiTheme] Native control=" + control.GetType().Name + ", dark=" + theme.IsDark + ", result=" + result);
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
                {
                    Trace.WriteLine("[GuiTheme] Native control theming unavailable: " + ex.Message);
                }
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string subApplicationName, string subIdList);

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
