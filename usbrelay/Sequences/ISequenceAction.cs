using System.Collections.Generic;

namespace usbrelay.Sequences
{
    public interface ISequenceAction
    {
        IEnumerable<RelayResource> Resources { get; }
        void Execute(SequenceExecutionContext context);
    }
}
