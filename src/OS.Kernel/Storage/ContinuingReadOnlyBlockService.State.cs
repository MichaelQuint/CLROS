using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Stable, zero-initialized, noncopyable retained owner. Pool DMA
    /// ownership stays Device across successful completions. A fault or failed
    /// Stop quarantines the backing: preserve everything and retry Stop, never
    /// submit/copy/reinitialize. A retired state cannot be started again.</summary>
    public struct State
    {
        internal ulong Self, PoolEpoch, LastClock, RequestGeneration, RequestSector, Deadline;
        internal Admission Input;
        internal DmaBufferAllocation Allocation;
        internal VirtioBlkQueueResult Queue;
        internal ulong Capacity, NotifyAddress;
        internal ushort AvailableIndex, UsedConsumed;
        internal byte ConfigGeneration, Phase;
        internal bool DeviceOwned, ReturnedToCpu, Scrubbed, Discard, Observing, ObservationReentered;
        internal Status Failure;
        internal byte RequestSectors;
        internal ulong SubmittedCount, CompletedCount, CopiedCount, CancelledCount, PollCount;
        public readonly bool Running => Phase is >= IdlePhase and <= ReadyPhase;
        public readonly bool ReadPending => Phase == PendingPhase;
        public readonly bool ReadReady => Phase == ReadyPhase;
        public readonly bool Retired => Phase == RetiredPhase;
        public readonly bool RequiresCleanup => Self != 0 && Phase is >= StartingPhase and <= FaultPhase;
        public readonly Status LastFailure => Failure;
        public readonly ulong SectorCount => Capacity;
        /// <summary>Last retained request's data length, not readiness or copy authority.</summary>
        public readonly uint RequestBytes => RequestSectors * VirtioBlkDmaLayout.DataBytes;
        public readonly ulong Submitted => SubmittedCount;
        public readonly ulong Completed => CompletedCount;
        public readonly ulong Copied => CopiedCount;
        public readonly ulong Cancelled => CancelledCount;
        public readonly ulong Polls => PollCount;
        public readonly ushort PublishedIndex => AvailableIndex;
        public readonly ushort ConsumedIndex => UsedConsumed;
        public readonly VirtioBlkQueueResult Receipt => Queue;
    }
}
