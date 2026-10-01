using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace usbrelay
{
    internal sealed class GuiMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly GuiTheme theme;

        public GuiMenuRenderer(GuiTheme theme) : base(new MenuColors(theme))
        {
            this.theme = theme;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? theme.Foreground : theme.Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? theme.Foreground : theme.Muted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            using (var pen = new Pen(theme.Accent, 2))
            {
                Rectangle area = e.ImageRectangle;
                e.Graphics.DrawLines(pen, new[]
                {
                    new Point(area.Left + 2, area.Top + area.Height / 2),
                    new Point(area.Left + area.Width / 2 - 1, area.Bottom - 4),
                    new Point(area.Right - 2, area.Top + 3)
                });
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            private readonly GuiTheme theme;
            public MenuColors(GuiTheme theme) { this.theme = theme; UseSystemColors = false; }
            public override Color MenuStripGradientBegin => theme.Background;
            public override Color MenuStripGradientEnd => theme.Background;
            public override Color ToolStripDropDownBackground => theme.Surface;
            public override Color ImageMarginGradientBegin => theme.Surface;
            public override Color ImageMarginGradientMiddle => theme.Surface;
            public override Color ImageMarginGradientEnd => theme.Surface;
            public override Color MenuItemSelected => theme.Hover;
            public override Color MenuItemSelectedGradientBegin => theme.Hover;
            public override Color MenuItemSelectedGradientEnd => theme.Hover;
            public override Color MenuItemPressedGradientBegin => theme.Selection;
            public override Color MenuItemPressedGradientMiddle => theme.Selection;
            public override Color MenuItemPressedGradientEnd => theme.Selection;
            public override Color MenuItemBorder => theme.Border;
            public override Color MenuBorder => theme.Border;
            public override Color ToolStripBorder => theme.Border;
            public override Color SeparatorDark => theme.Border;
            public override Color SeparatorLight => theme.Surface;
            public override Color CheckBackground => theme.Surface;
            public override Color CheckSelectedBackground => theme.Hover;
            public override Color CheckPressedBackground => theme.Selection;
        }
    }

    internal sealed class RelayChannelButton : Button
    {
        private readonly string channelName;
        private readonly bool on;
        private bool mousePressed;
        private bool keyboardPressed;
        public GuiTheme Theme { get; set; }

        public RelayChannelButton(GuiTheme theme, string channelName, bool on)
        {
            Theme = theme;
            this.channelName = channelName;
            this.on = on;
            Text = channelName + Environment.NewLine + (on ? "ON" : "OFF");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Theme == null) { base.OnPaint(e); return; }
            bool hovered = Enabled && ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color background = keyboardPressed || (hovered && mousePressed) ? Theme.Selection : hovered ? Theme.Hover : BackColor;
            using (var brush = new SolidBrush(background))
            using (var pen = new Pen(Theme.Border))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
                if (Width > 1 && Height > 1) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
            if (Width <= 8 || Height <= 12) return;

            int stateHeight = Math.Min(Height - 8, Font.Height + 2);
            Color foreground = Enabled ? ForeColor : Theme.Muted;
            TextRenderer.DrawText(e.Graphics, channelName, Font,
                new Rectangle(4, 4, Width - 8, Math.Max(0, Height - stateHeight - 12)), foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, on ? "ON" : "OFF", Font,
                new Rectangle(4, Height - stateHeight - 4, Width - 8, stateHeight), foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), foreground, background);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { mousePressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            mousePressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) { mousePressed = false; Invalidate(); }
            base.OnMouseCaptureChanged(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { keyboardPressed = true; Invalidate(); }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            keyboardPressed = false;
            Invalidate();
            base.OnKeyUp(e);
        }
        protected override void OnLostFocus(EventArgs e)
        {
            keyboardPressed = false;
            Invalidate();
            base.OnLostFocus(e);
        }
    }

    internal sealed class RelayCard : GroupBox
    {
        public GuiTheme Theme { get; set; }
        public override Rectangle DisplayRectangle => new Rectangle(Padding.Left, Padding.Top,
            System.Math.Max(0, ClientSize.Width - Padding.Horizontal), System.Math.Max(0, ClientSize.Height - Padding.Vertical));

        public RelayCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Theme == null) { base.OnPaint(e); return; }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = 6;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width < radius * 2 || bounds.Height < radius * 2) return;
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(Theme.Surface))
            using (var pen = new Pen(Theme.Border))
            {
                path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
                path.AddArc(bounds.Right - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
                path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(12, 8, Width - 24, 22), Theme.Foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
