using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

public static unsafe partial class WritableBlockReadPort
{
    /// <summary>Zeroed noncopyable POD. Faults retain exact original ownership;
    /// no replacement device at the same address is adopted. An uncertain issued
    /// request retires only after the original owner is independently stopped.
    /// Retired local metadata grants no further retained-storage authority.</summary>
    public struct State
    {
        internal ulong Self, Generation, Sector;
        internal Admission Input;
        internal B.RequestToken Io;
        internal B.Completion Physical;
        internal B.Status Failure;
        internal byte Phase;
        internal bool HasIo, CancelRequested;
        public readonly bool RequestActive => HasIo;
        public readonly bool Ready => Phase == ReadyPhase;
        public readonly bool Retired => Phase == RetiredPhase;
        public readonly B.Status LastBlockFailure => Failure;
    }
}
