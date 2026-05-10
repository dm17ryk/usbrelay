using System.Runtime.Serialization;

namespace usbrelay.Sequences
{
    [DataContract]
    public sealed class SequenceDefinition
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public string RunButtonText { get; set; }

        [DataMember(Order = 3)]
        public string Description { get; set; }

        [DataMember(Order = 4)]
        public string Script { get; set; }

        public string DisplayRunButtonText => string.IsNullOrWhiteSpace(RunButtonText) ? "Run" : RunButtonText;
    }
}
