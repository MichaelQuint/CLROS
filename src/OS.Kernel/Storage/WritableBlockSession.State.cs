using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    /// <summary>Stable zero-initialized noncopyable owner. One device-owned DMA
    /// lease remains retained between completions. Fault/uncertain Stop never
    /// frees it. Stop only establishes quiescence, scrubs and returns the lease;
    /// it never flushes or advances durability. Dirty/retry/first-error facts
    /// survive retirement. Restart requires a fresh zero owner and unique epoch.</summary>
    public struct State
    {
        internal ulong Self, PoolEpoch, LastClock, Generation, Sector, Deadline, FlushTarget;
        internal Admission Input;
        internal DmaBufferAllocation Allocation;
        internal VirtioBlkQueueResult Queue;
        internal ulong Capacity, NotifyAddress;
        internal ulong SubmittedCount, CompletedCount, SubmittedWriteBytes, CompletedWriteBytes, FlushedWriteBytes;
        internal ushort AvailableIndex, UsedConsumed;
        internal byte ConfigGeneration, Phase;
        internal Operation Kind;
        internal bool DeviceOwned, ReturnedToCpu, Scrubbed, CancelRequested, Retry;
        internal Status Failure;
        public readonly bool Running => Phase is IdlePhase or PendingPhase or ReadReadyPhase;
        /// <summary>Queue ownership is busy through both an in-flight request
        /// and an unconsumed read result. Legacy writer/mutation admission must
        /// not treat retained ReadReady DMA as an idle device.</summary>
        public readonly bool RequestPending => Phase is PendingPhase or ReadReadyPhase;
        public readonly bool ReadPending => Phase == PendingPhase && Kind == Operation.Read;
        public readonly bool ReadReady => Phase == ReadReadyPhase;
        public readonly bool Retired => Phase == RetiredPhase;
        public readonly bool RequiresCleanup => Self != 0 && (Phase is >= StartingPhase and <= FaultPhase || Phase == ReadReadyPhase);
        public readonly Status LastFailure => Failure;
        public readonly ulong SectorCount => Capacity;
        public readonly ulong Submitted => SubmittedCount;
        public readonly ulong Completed => CompletedCount;
        public readonly ulong SubmittedBytes => SubmittedWriteBytes;
        public readonly ulong CompletedBytes => CompletedWriteBytes;
        public readonly ulong DurableBytes => FlushedWriteBytes;
        public readonly bool Dirty => SubmittedWriteBytes != FlushedWriteBytes;
        public readonly bool RetryRequired => Retry;
        public readonly Operation LastOperation => Kind;
        public readonly ulong LastSector => Sector;
        public readonly VirtioBlkQueueResult Receipt => Queue;
    }
}
