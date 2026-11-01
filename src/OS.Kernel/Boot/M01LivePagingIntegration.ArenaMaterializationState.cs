namespace OS.Boot;
public static unsafe partial class M01LivePagingIntegration
{
    private struct ArenaMaterializationState
    {
        internal OS.Kernel.Memory.X64PageTableEntryStatus PageTableEntryStatus;
        internal OS.Kernel.Memory.X64PageMapStatus PageImageStatus;
        internal PhysicalAddress RootTable;
        internal ulong TablePageCount;
        internal ulong VerifiedPageCount;
        internal ulong EntryCount;
        internal ulong VerifiedEntryCount;
        internal ulong InvalidPageOrdinal;
        internal ulong InvalidEntryIndex;
        internal ulong ExpectedEntry;
        internal ulong ObservedEntry;
        internal ulong SourceFingerprint;
        internal ulong ObservedFingerprint;
        internal ulong StateFingerprintBefore;
        internal ulong StateFingerprintAfter;
        internal ulong BytesWritten;
        internal ulong ReachablePageCount;
        internal ulong MappedLeafCount;
        internal ulong VerifiedStateByteCount;
        internal ulong InvalidStateByteIndex;
    }
}
