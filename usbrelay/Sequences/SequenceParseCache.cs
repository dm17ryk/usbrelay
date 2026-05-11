using System;
using System.Collections.Generic;

namespace usbrelay.Sequences
{
    public sealed class SequenceParseCache
    {
        private readonly Func<string, SequenceParseResult> parser;
        private readonly Dictionary<SequenceDefinition, CacheEntry> entries = new Dictionary<SequenceDefinition, CacheEntry>();

        public SequenceParseCache()
            : this(SequenceParser.Parse)
        {
        }

        public SequenceParseCache(Func<string, SequenceParseResult> parser)
        {
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            this.parser = parser;
        }

        public SequenceParseResult Get(SequenceDefinition sequence)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            string script = sequence.Script ?? string.Empty;
            CacheEntry entry;
            if (entries.TryGetValue(sequence, out entry) && string.Equals(entry.Script, script, StringComparison.Ordinal))
                return entry.Result;

            var result = parser(script);
            entries[sequence] = new CacheEntry(script, result);
            return result;
        }

        public void Retain(IEnumerable<SequenceDefinition> activeSequences)
        {
            var active = new HashSet<SequenceDefinition>(activeSequences ?? Array.Empty<SequenceDefinition>());
            var stale = new List<SequenceDefinition>();

            foreach (var sequence in entries.Keys)
            {
                if (!active.Contains(sequence))
                    stale.Add(sequence);
            }

            foreach (var sequence in stale)
                entries.Remove(sequence);
        }

        private sealed class CacheEntry
        {
            public CacheEntry(string script, SequenceParseResult result)
            {
                Script = script;
                Result = result;
            }

            public string Script { get; }
            public SequenceParseResult Result { get; }
        }
    }
}
