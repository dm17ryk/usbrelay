using System;
using System.Drawing;
using System.Windows.Forms;

namespace usbrelay
{
    public sealed class SplashForm : Form
    {
        private readonly Label statusLabel = new Label();
        private Image splashImage;

        public SplashForm()
        {
            Text = "USB Relay Control";
            Width = 640;
            Height = 360;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(18, 22, 28);

            Icon icon = AppAssets.LoadApplicationIcon();
            if (icon != null)
                Icon = icon;

            splashImage = AppAssets.LoadSplashImage();
            if (splashImage != null)
            {
                Controls.Add(new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = splashImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = BackColor
                });
            }

            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Height = 34;
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.BackColor = Color.FromArgb(28, 32, 38);
            statusLabel.ForeColor = Color.WhiteSmoke;
            statusLabel.Font = new Font("Segoe UI", 9F);
            statusLabel.Text = "Starting...";
            Controls.Add(statusLabel);
            statusLabel.BringToFront();
        }

        public void SetStatus(string status)
        {
            statusLabel.Text = status;
            Refresh();
            Application.DoEvents();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && splashImage != null)
            {
                splashImage.Dispose();
                splashImage = null;
            }

            base.Dispose(disposing);
        }
    }
}
