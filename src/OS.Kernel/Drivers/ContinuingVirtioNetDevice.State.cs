using RingEngine = global::OS.Drivers.ContinuingVirtioNet;

namespace OS.Drivers;

public static unsafe partial class ContinuingVirtioNetDevice
{
    /// <summary>Exclusive noncopyable owner. Start failures attempt bounded Stop;
    /// if RequiresCleanup remains true, preserve every borrowed resource and retry
    /// Stop only after resolving the reported cleanup failure. Readonly properties
    /// are observations, not handles or independent device authority.</summary>

    public struct State
    {
        internal ulong Self, PoolEpoch, LastClock;
        internal Admission Input;
        internal DmaBufferAllocation Allocation;
        internal RingEngine.State Ring;
        internal VirtioNetQueueResult Queue;
        internal fixed ulong Deadlines[8];
        internal byte Phase;
        internal bool DeviceOwned, RingBound, RingReleased, ReturnedToCpu;
        internal Status Failure;
        internal RingEngine.Status RingResult;
        public readonly bool Running => Phase == 2;
        public readonly bool Retired => Phase == 4;
        public readonly bool RequiresCleanup => Self != 0 && Phase is >= 1 and <= 3;
        public readonly Status LastFailure => Failure;
        public readonly RingEngine.Status LastRingStatus => RingResult;
        public readonly VirtioNetQueueResult Receipt => Queue;
        public readonly ulong ReceivedFrames => Ring.ReceivedFrames;
        public readonly ulong DroppedFrames => Ring.DroppedFrames;
        public readonly ulong ReclaimedTransmits => Ring.ReclaimedTransmits;
    }
}
