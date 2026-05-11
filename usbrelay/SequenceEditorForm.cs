using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using usbrelay.Sequences;

namespace usbrelay
{
    public sealed class SequenceEditorForm : Form
    {
        private readonly TextBox nameTextBox = new TextBox();
        private readonly TextBox runButtonTextBox = new TextBox();
        private readonly TextBox descriptionTextBox = new TextBox();
        private readonly TextBox diagnosticsTextBox = new TextBox();
        private readonly TextEditor editor = new TextEditor();
        private readonly string layoutSettingsPath;
        private CompletionWindow completionWindow;
        private SplitContainer splitContainer;

        public SequenceEditorForm(SequenceDefinition sequence)
            : this(sequence, SequenceEditorLayoutSettings.DefaultPath)
        {
        }

        public SequenceEditorForm(SequenceDefinition sequence, string layoutSettingsPath)
        {
            this.layoutSettingsPath = layoutSettingsPath;
            InitializeComponent();
            LoadSequence(sequence);
        }

        public SequenceDefinition Sequence { get; private set; }

        private void InitializeComponent()
        {
            Text = "Sequence Editor";
            Width = 980;
            Height = 620;
            MinimumSize = new Size(780, 460);
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            Icon icon = AppAssets.LoadApplicationIcon();
            if (icon != null)
                Icon = icon;

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 280
            };

            splitContainer.Panel1.Controls.Add(CreatePropertiesPanel());
            splitContainer.Panel2.Controls.Add(CreateEditorPanel());
            Controls.Add(splitContainer);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadLayoutSettings();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveLayoutSettings();
            base.OnFormClosing(e);
        }

        private Control CreatePropertiesPanel()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 1,
                RowCount = 9
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label { Text = "Name", AutoSize = true }, 0, 0);
            nameTextBox.Dock = DockStyle.Top;
            root.Controls.Add(nameTextBox, 0, 1);

            root.Controls.Add(new Label { Text = "Run button", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 2);
            runButtonTextBox.Dock = DockStyle.Top;
            root.Controls.Add(runButtonTextBox, 0, 3);

            root.Controls.Add(new Label { Text = "Description", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 4);
            descriptionTextBox.Dock = DockStyle.Fill;
            descriptionTextBox.Multiline = true;
            root.Controls.Add(descriptionTextBox, 0, 5);

            root.Controls.Add(new Label { Text = "Validation", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 6);
            diagnosticsTextBox.Dock = DockStyle.Fill;
            diagnosticsTextBox.Multiline = true;
            diagnosticsTextBox.ReadOnly = true;
            root.Controls.Add(diagnosticsTextBox, 0, 7);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            buttons.Controls.Add(MiniButton("Test Sequence", (s, e) => ValidateScript()));
            buttons.Controls.Add(MiniButton("Save", (s, e) => SaveAndClose()));
            buttons.Controls.Add(MiniButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel));
            root.Controls.Add(buttons, 0, 8);

            return root;
        }

        private Control CreateEditorPanel()
        {
            editor.ShowLineNumbers = true;
            editor.WordWrap = false;
            editor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            editor.FontSize = 13;
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            editor.TextArea.TextEntered += TextArea_TextEntered;

            return new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = editor
            };
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

        private void LoadSequence(SequenceDefinition sequence)
        {
            nameTextBox.Text = sequence?.Name ?? "New sequence";
            runButtonTextBox.Text = sequence?.RunButtonText ?? "Run";
            descriptionTextBox.Text = sequence?.Description ?? string.Empty;
            editor.Text = sequence?.Script ?? "sequence.PowerOff(\"6QMBS\", 1);" + Environment.NewLine;
            ValidateScript();
        }

        private void ValidateScript()
        {
            var result = SequenceParser.Parse(editor.Text);
            diagnosticsTextBox.Text = result.IsValid
                ? "Validation OK" + Environment.NewLine + "Resources: " + string.Join(", ", result.Resources.Select(resource => resource.ToString()))
                : string.Join(Environment.NewLine, result.Diagnostics);
        }

        private void SaveAndClose()
        {
            ValidateScript();
            var result = SequenceParser.Parse(editor.Text);
            if (!result.IsValid)
            {
                MessageBox.Show(this, "Fix validation errors before saving.", "Sequence validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Sequence = new SequenceDefinition
            {
                Name = string.IsNullOrWhiteSpace(nameTextBox.Text) ? "Sequence" : nameTextBox.Text.Trim(),
                RunButtonText = string.IsNullOrWhiteSpace(runButtonTextBox.Text) ? "Run" : runButtonTextBox.Text.Trim(),
                Description = descriptionTextBox.Text,
                Script = editor.Text
            };
            DialogResult = DialogResult.OK;
        }

        private void LoadLayoutSettings()
        {
            if (string.IsNullOrEmpty(layoutSettingsPath))
                return;

            SequenceEditorLayoutSettings settings;
            try
            {
                settings = SequenceEditorLayoutSettings.Load(layoutSettingsPath);
            }
            catch
            {
                return;
            }

            if (settings == null)
                return;

            if (settings.Width >= MinimumSize.Width && settings.Height >= MinimumSize.Height)
            {
                StartPosition = FormStartPosition.Manual;
                SetBounds(settings.Left, settings.Top, settings.Width, settings.Height);
            }

            if (settings.SplitterDistance > 0 && splitContainer.Width > 0)
                splitContainer.SplitterDistance = Math.Min(settings.SplitterDistance, Math.Max(splitContainer.Panel1MinSize, splitContainer.Width - splitContainer.Panel2MinSize));

            if (settings.WindowState != FormWindowState.Minimized)
                WindowState = settings.WindowState;
        }

        private void SaveLayoutSettings()
        {
            if (string.IsNullOrEmpty(layoutSettingsPath))
                return;

            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var settings = new SequenceEditorLayoutSettings
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                WindowState = WindowState,
                SplitterDistance = splitContainer.SplitterDistance
            };
            settings.Save(layoutSettingsPath);
        }

        private void TextArea_TextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text != ".")
                return;

            completionWindow = new CompletionWindow(editor.TextArea);
            var data = completionWindow.CompletionList.CompletionData;
            data.Add(new CompletionData("PowerOn(\"6QMBS\", 1)", "Turn a relay channel on"));
            data.Add(new CompletionData("PowerOff(\"6QMBS\", 1)", "Turn a relay channel off"));
            data.Add(new CompletionData("Sleep(500)", "Pause sequence execution"));
            data.Add(new CompletionData("ReadChannel(\"6QMBS\", 1)", "Read channel status"));
            data.Add(new CompletionData("WaitChannel(\"6QMBS\", 1, RelayState.On, 3000)", "Wait for channel status"));
            data.Add(new CompletionData("RunTool(\"tool.exe\", \"--args\")", "Run external process and wait for exit"));
            data.Add(new CompletionData("Fail(\"message\")", "Fail the current sequence"));
            completionWindow.Show();
            completionWindow.Closed += (s, args) => completionWindow = null;
        }

        private sealed class CompletionData : ICompletionData
        {
            public CompletionData(string text, string description)
            {
                Text = text;
                Description = description;
            }

            public System.Windows.Media.ImageSource Image => null;
            public string Text { get; }
            public object Content => Text;
            public object Description { get; }
            public double Priority => 0;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                textArea.Document.Replace(completionSegment, Text);
            }
        }
    }
}
