using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace usbrelay
{
    [DataContract]
    public sealed class MainLayoutSettings
    {
        [DataMember(Order = 1)]
        public int Left { get; set; }

        [DataMember(Order = 2)]
        public int Top { get; set; }

        [DataMember(Order = 3)]
        public int Width { get; set; }

        [DataMember(Order = 4)]
        public int Height { get; set; }

        [DataMember(Order = 5)]
        public FormWindowState WindowState { get; set; }

        [DataMember(Order = 6)]
        public int MainSplitterDistance { get; set; }

        [DataMember(Order = 7)]
        public int SequenceListPercent { get; set; }

        [DataMember(Order = 8)]
        public int DeviceListPercent { get; set; }

        public static string DefaultPath
        {
            get
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "usbrelay", "layout.json");
            }
        }

        public static MainLayoutSettings Load(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(MainLayoutSettings));
                return (MainLayoutSettings)serializer.ReadObject(stream);
            }
        }

        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(MainLayoutSettings));
                serializer.WriteObject(stream, this);
            }
        }
    }
}
