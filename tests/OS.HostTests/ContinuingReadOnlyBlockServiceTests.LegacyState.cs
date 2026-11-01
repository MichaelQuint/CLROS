using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    // Exact pre-extension managed field sequence. No production layout helper
    // or serialized ABI is introduced to test the former tail padding.
#pragma warning disable CS0649
    private struct LegacyState
    {
        internal ulong Self, PoolEpoch, LastClock, RequestGeneration, RequestSector, Deadline;
        internal Block.Admission Input;
        internal DmaBufferAllocation Allocation;
        internal VirtioBlkQueueResult Queue;
        internal ulong Capacity, NotifyAddress;
        internal ushort AvailableIndex, UsedConsumed;
        internal byte ConfigGeneration, Phase;
        internal bool DeviceOwned, ReturnedToCpu, Scrubbed, Discard, Observing, ObservationReentered;
        internal Block.Status Failure;
        internal ulong SubmittedCount, CompletedCount, CopiedCount, CancelledCount, PollCount;
    }
#pragma warning restore CS0649
}
