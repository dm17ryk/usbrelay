using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace usbrelay
{
    internal static class AppAssets
    {
        public static Icon LoadApplicationIcon()
        {
            try
            {
                string iconPath = FindAssetPath(Path.Combine("assets", "icons", "usbrelay.ico"));
                if (iconPath != null)
                    return new Icon(iconPath);

                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        public static Image LoadSplashImage()
        {
            string splashPath = FindAssetPath(Path.Combine("assets", "splash", "usbrelay-splash-desktop-1366x768.png"));
            if (splashPath == null)
                return null;

            using (var stream = File.OpenRead(splashPath))
            using (var image = Image.FromStream(stream))
            {
                return new Bitmap(image);
            }
        }

        private static string FindAssetPath(string relativePath)
        {
            string[] roots =
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "usbrelay"))
            };

            foreach (string root in roots)
            {
                string candidate = Path.Combine(root, relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
