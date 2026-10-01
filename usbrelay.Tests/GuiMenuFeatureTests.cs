using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using usbrelay.Sequences;

namespace usbrelay.Tests
{
    internal static class GuiMenuFeatureTests
    {
        public static void Run()
        {
            var tests = new Action[] { MainMenusRouteOperationalActions, SequenceMenusRespectSelectionAndCancellation,
                ThemeMenuPersistsWithoutRediscovery, MenuRendererUpdatesNestedMenus, DialogsShowHelpVersionAndKeyboardClose,
                MenuActionsOpenAuxiliaryDialogs, PaletteKeepsTextReadable, ReapplyingThemeKeepsComboMetadata,
                SelectedStatusTextStaysReadable, StartupSelectionEnablesMenus };
            foreach (Action test in tests) { test(); Console.WriteLine("PASS " + test.Method.Name); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string TempPath(string file) => Path.Combine(Path.GetTempPath(), "usbrelay-gui-tests-" + Guid.NewGuid().ToString("N"), file);
        private static ToolStripMenuItem Item(MainForm form, string name) => (ToolStripMenuItem)form.MainMenuStrip.Items.Find(name, true).Single();
        private static MainForm CreateForm(FakeRelayBackend backend, SequenceRepository repository = null, Func<SequenceDefinition, bool> confirmation = null, string themePath = null)
        {
            return new MainForm(backend, repository ?? new SequenceRepository(TempPath("sequences.json")), TempPath("layout.json"), confirmation, null, themePath ?? TempPath("theme.json"));
        }

        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child)) yield return descendant;
            }
        }

        private static void MainMenusRouteOperationalActions()
        {
            var backend = new FakeRelayBackend(new RelayDevice("GUI-MENU", RelayDeviceType.TwoChannel, 2, 3));
            using (var form = CreateForm(backend))
            {
                form.PrepareForDisplay();
                Check(form.MainMenuStrip.Items.Count == 5, "Expected Sequences, Devices, View, Tools, and Help menus");
                Check(!Descendants(form).OfType<ComboBox>().Any(), "Theme selector must be in the menu");
                Check(Descendants(form).OfType<Button>().Count() == 2, "Only operational channel buttons should remain in the main view");
                int calls = backend.EnumerateDevicesCallCount;
                Item(form, "refreshDevicesMenuItem").PerformClick();
                Check(backend.EnumerateDevicesCallCount == calls + 1, "Refresh menu must discover devices");
                Check(Item(form, "refreshDevicesMenuItem").ShortcutKeys == Keys.F5, "Refresh must support F5");
                Item(form, "allOffMenuItem").PerformClick();
                Check(backend.GetDevice("GUI-MENU").StatusMask == 0, "All off menu must switch the connected board off");
            }
        }

        private static void SequenceMenusRespectSelectionAndCancellation()
        {
            var repository = new SequenceRepository(TempPath("sequences.json"));
            repository.Save(new[] { new SequenceDefinition { Name = "Keep", Script = "sequence.Sleep(0);" } });
            using (var form = CreateForm(new FakeRelayBackend(), repository, sequence => false))
            {
                Check(!Item(form, "editSequenceMenuItem").Enabled && !Item(form, "removeSequenceMenuItem").Enabled, "Edit/remove must be disabled without selection");
                form.PrepareForDisplay();
                var grid = Descendants(form).OfType<DataGridView>().Single(control => control.AccessibleName == "Saved sequences");
                grid.Rows[0].Selected = true;
                Check(Item(form, "removeSequenceMenuItem").Enabled, "Selected sequence must enable removal");
                Item(form, "removeSequenceMenuItem").PerformClick();
                Check(repository.Load().Count == 1, "Cancelled removal must preserve sequence");
                grid.ClearSelection();
                Check(!Item(form, "removeSequenceMenuItem").Enabled, "Clearing selection must disable removal");
            }
            using (var form = CreateForm(new FakeRelayBackend(), repository, sequence => true))
            {
                form.PrepareForDisplay();
                Descendants(form).OfType<DataGridView>().Single(control => control.AccessibleName == "Saved sequences").Rows[0].Selected = true;
                Item(form, "removeSequenceMenuItem").PerformClick();
                Check(repository.Load().Count == 0 && !Item(form, "removeSequenceMenuItem").Enabled, "Confirmed removal must persist and disable edit/remove");
            }
        }

        private static void ThemeMenuPersistsWithoutRediscovery()
        {
            string path = TempPath("theme.json");
            var backend = new FakeRelayBackend(new RelayDevice("GUI-THEME", RelayDeviceType.TwoChannel, 2, 1));
            using (var form = CreateForm(backend, themePath: path))
            {
                form.PrepareForDisplay();
                int calls = backend.EnumerateDevicesCallCount;
                Check(Item(form, "darkThemeMenuItem").Checked && !Item(form, "lightThemeMenuItem").Checked, "Dark must be selected initially");
                Item(form, "lightThemeMenuItem").PerformClick();
                Check(ThemeSettings.Load(path).Theme == "light" && !Item(form, "darkThemeMenuItem").Checked && Item(form, "lightThemeMenuItem").Checked, "Theme menu must persist and check only the active theme");
                Check(form.BackColor == new GuiTheme("light").Background, "Light menu must recolor the main form");
                Item(form, "darkThemeMenuItem").PerformClick();
                Check(ThemeSettings.Load(path).Theme == "dark", "Dark theme must persist");
                Check(backend.EnumerateDevicesCallCount == calls && backend.GetDevice("GUI-THEME").StatusMask == 1, "Theme changes must preserve hardware state and avoid rediscovery");
                using (var reopened = CreateForm(new FakeRelayBackend(), themePath: path))
                    Check(Item(reopened, "darkThemeMenuItem").Checked, "Reopened form must restore the saved theme");
            }
        }

        private static void MenuRendererUpdatesNestedMenus()
        {
            using (var form = CreateForm(new FakeRelayBackend()))
            {
                foreach (string name in new[] { "light", "dark", "light" })
                {
                    var theme = new GuiTheme(name);
                    theme.Apply(form);
                    var item = Item(form, "themeMenuItem");
                    var renderer = (ToolStripProfessionalRenderer)item.DropDown.Renderer;
                    Check(item.DropDown.BackColor == theme.Surface && renderer.ColorTable.MenuItemSelected == theme.Hover, "Nested menus must update their complete palette");
                }
            }
        }

        private static void DialogsShowHelpVersionAndKeyboardClose()
        {
            foreach (string name in new[] { "dark", "light" })
            {
                var theme = new GuiTheme(name);
                using (var about = new AboutForm(theme))
                using (var help = new HelpForm(theme))
                {
                    Check(Descendants(about).OfType<Label>().Any(label => label.Text == "Version " + AboutForm.ApplicationVersion), "About must display the built application version");
                    Check(about.BackColor == theme.Background && help.BackColor == theme.Background, "Help/About must use the active theme");
                    Check(about.AcceptButton != null && about.CancelButton != null && help.CancelButton != null, "Dialogs must close from Enter/Escape");
                    string text = Descendants(help).OfType<RichTextBox>().Single().Text;
                    Check(text.Contains("F5") && text.Contains("Ctrl+N") && text.Contains("MCP") && text.Contains("usbrelay --help"), "Offline help must explain menus, shortcuts, MCP, and CLI");
                }
            }
        }

        private static void MenuActionsOpenAuxiliaryDialogs()
        {
            using (var form = CreateForm(new FakeRelayBackend()))
            {
                CheckDialog(form, "aboutMenuItem", nameof(AboutForm));
                CheckDialog(form, "helpMenuItem", nameof(HelpForm));
                CheckDialog(form, "aiToolsMenuItem", "IntegrationForm");
            }
        }

        private static void CheckDialog(MainForm form, string item, string expected)
        {
            bool opened = false;
            using (var timer = new Timer { Interval = 100 })
            {
                timer.Tick += (s, e) =>
                {
                    Form dialog = form.OwnedForms.FirstOrDefault();
                    if (dialog == null) return;
                    opened = dialog.GetType().FullName == "usbrelay." + expected;
                    dialog.Close();
                    timer.Stop();
                };
                timer.Start();
                Item(form, item).PerformClick();
                Check(opened, "Menu action did not open " + expected);
            }
        }

        private static void PaletteKeepsTextReadable()
        {
            foreach (string name in new[] { "dark", "light" })
            {
                var theme = new GuiTheme(name);
                foreach (Color surface in new[] { theme.Background, theme.Surface, theme.Selection })
                    Check(Contrast(theme.Foreground, surface) >= 4.5, "Normal text must have readable contrast in " + name);
                Check(Contrast(theme.Muted, theme.Surface) >= 4.5 && Contrast(theme.OnForeground, theme.OnBackground) >= 4.5, "Status and secondary text must have readable contrast in " + name);
            }
        }

        private static double Contrast(Color first, Color second)
        {
            double a = Luminance(first), b = Luminance(second);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        private static double Luminance(Color color)
        {
            Func<byte, double> component = value => value / 255.0 <= 0.04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * component(color.R) + 0.7152 * component(color.G) + 0.0722 * component(color.B);
        }

        private static void ReapplyingThemeKeepsComboMetadata()
        {
            object metadata = new object();
            using (var combo = new ComboBox { Tag = metadata })
            {
                new GuiTheme("dark").Apply(combo);
                new GuiTheme("light").Apply(combo);
                Check(ReferenceEquals(metadata, combo.Tag), "Theme must preserve application control metadata");
            }
        }

        private static void SelectedStatusTextStaysReadable()
        {
            using (var form = CreateForm(new FakeRelayBackend(new RelayDevice("GUI-STATUS", RelayDeviceType.TwoChannel, 2, 1))))
            {
                form.PrepareForDisplay();
                var grid = Descendants(form).OfType<DataGridView>().Single(control => control.AccessibleName == "Device and channel status");
                foreach (string theme in new[] { "light", "dark" })
                {
                    Item(form, theme + "ThemeMenuItem").PerformClick();
                    for (int column = 4; column <= 5; column++)
                    {
                        var args = new DataGridViewCellFormattingEventArgs(column, 0, grid.Rows[0].Cells[column].Value, typeof(string), new DataGridViewCellStyle());
                        typeof(DataGridView).GetMethod("OnCellFormatting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                            null, new[] { typeof(DataGridViewCellFormattingEventArgs) }, null).Invoke(grid, new object[] { args });
                        Check(Contrast(args.CellStyle.SelectionForeColor, args.CellStyle.SelectionBackColor) >= 4.5, "Selected ON/OFF status must remain readable in " + theme);
                    }
                }
            }
        }

        private static void StartupSelectionEnablesMenus()
        {
            var repository = new SequenceRepository(TempPath("sequences.json"));
            repository.Save(new[] { new SequenceDefinition { Name = "First", Script = "sequence.Sleep(0);" }, new SequenceDefinition { Name = "Second", Script = "sequence.Sleep(0);" } });
            using (var form = CreateForm(new FakeRelayBackend(), repository))
            {
                form.Show();
                Application.DoEvents();
                var grid = Descendants(form).OfType<DataGridView>().Single(control => control.AccessibleName == "Saved sequences");
                Check(grid.SelectedRows.Count == 1 && Item(form, "editSequenceMenuItem").Enabled, "Startup selection must enable Edit without an extra click");
                grid.Rows[1].Selected = true;
                typeof(MainForm).GetMethod("RenderSequences", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(form, null);
                Check(((SequenceDefinition)grid.SelectedRows[0].Tag).Name == "Second", "Rerendering must preserve the selected sequence");
                form.Close();
            }
        }
    }
}
