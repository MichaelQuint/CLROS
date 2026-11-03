using System;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxWritableSyscallObservation
{
    /// <summary>Noncopyable-by-contract fixed batch. No external buffer pointer
    /// or Span is retained. Two records share8192 append-only bytes; an ACK does
    /// not recycle storage until the whole batch drains. Evidence loss persists
    /// through drain/Stop. These properties are observations, not permissions
    /// to access backing after its enclosing owner releases it.</summary>

    public struct State
    {
        internal ulong Self;
        internal Admission Input;
        internal ulong Sequence;
        internal uint Count, Head, Used;
        internal byte Phase;
        internal EvidenceFailure Failure;
        internal ushort Reserved;
        internal Header First, Second;
        internal fixed byte Data[PayloadCapacity];
        public readonly bool Active => Phase == 1;
        public readonly bool Retired => Phase == 2;
        public readonly bool EvidenceLost => Failure != EvidenceFailure.None;
        public readonly EvidenceFailure FailureReason => Failure;
        public readonly uint PendingRecords => Count - Head;
    }
}
