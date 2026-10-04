using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace usbrelay
{
    [DataContract]
    public sealed class UpdateSettings
    {
        [DataMember]
        public bool AutomaticChecks { get; set; } = true;

        [DataMember(EmitDefaultValue = false)]
        public DateTime LastCheckUtc { get; set; }

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "usbrelay", "updates.json");

        public bool IsCheckDue(DateTime nowUtc) => AutomaticChecks &&
            (LastCheckUtc == default(DateTime) || nowUtc < LastCheckUtc || nowUtc - LastCheckUtc >= TimeSpan.FromHours(24));

        public static UpdateSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                Trace.WriteLine("[UpdateSettings] No saved settings; automatic checks enabled: " + path);
                return new UpdateSettings();
            }
            try
            {
                using (var stream = File.OpenRead(path))
                    return (UpdateSettings)new DataContractJsonSerializer(typeof(UpdateSettings)).ReadObject(stream)
                        ?? new UpdateSettings();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[UpdateSettings] Read failed; using defaults: " + ex);
                return new UpdateSettings();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var stream = File.Create(path))
                new DataContractJsonSerializer(typeof(UpdateSettings)).WriteObject(stream, this);
            Trace.WriteLine("[UpdateSettings] Saved automatic=" + AutomaticChecks + ", last check=" + LastCheckUtc.ToString("O"));
        }
    }
}
