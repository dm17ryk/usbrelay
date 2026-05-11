using System.Collections.Generic;
using System.Linq;

namespace usbrelay.Sequences
{
    public sealed class SequenceParseResult
    {
        public SequenceParseResult(IEnumerable<ISequenceAction> actions, IEnumerable<string> diagnostics)
        {
            Actions = new List<ISequenceAction>(actions);
            Diagnostics = new List<string>(diagnostics);
            Resources = new HashSet<RelayResource>(Actions.SelectMany(action => action.Resources));
        }

        public IReadOnlyList<ISequenceAction> Actions { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public ISet<RelayResource> Resources { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }
}
