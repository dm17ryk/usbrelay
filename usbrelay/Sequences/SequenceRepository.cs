using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace usbrelay.Sequences
{
    public sealed class SequenceRepository
    {
        private readonly string path;

        public SequenceRepository(string path)
        {
            this.path = path;
        }

        public static string DefaultPath
        {
            get
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "usbrelay", "sequences.json");
            }
        }

        public IReadOnlyList<SequenceDefinition> Load()
        {
            if (!File.Exists(path))
                return new List<SequenceDefinition>();

            using (var stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<SequenceDefinition>));
                return (List<SequenceDefinition>)serializer.ReadObject(stream);
            }
        }

        public void Save(IEnumerable<SequenceDefinition> sequences)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<SequenceDefinition>));
                serializer.WriteObject(stream, new List<SequenceDefinition>(sequences));
            }
        }
    }
}
