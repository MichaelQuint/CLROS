using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Copied diagnostic facts, not ownership authority. Sequence is
    /// the submitted/completed count, or the next copied count for CopyReady.
    /// CopyReady is a candidate: only a later matching State.Copied count proves
    /// that its bytes were actually copied. Cancelled completions expose zero.</summary>
    public readonly struct Observation
    {
        public readonly ObservationKind Kind;
        public readonly ulong Owner, Epoch, Generation, Sector, Sequence;
        public readonly uint Bytes;
        public readonly ushort AvailableIndex, ConsumedIndex;
        internal Observation(State* s, ObservationKind kind, ulong sequence, uint bytes)
        {
            Kind = kind;
            Owner = (ulong)s;
            Epoch = s->Input.Epoch;
            Generation = s->RequestGeneration;
            Sector = s->RequestSector;
            Sequence = sequence;
            Bytes = bytes;
            AvailableIndex = s->AvailableIndex;
            ConsumedIndex = s->UsedConsumed;
        }
    }
}
