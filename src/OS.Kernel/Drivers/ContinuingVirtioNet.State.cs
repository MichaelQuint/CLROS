using OS.Kernel.Platform;

namespace OS.Drivers;
public static unsafe partial class ContinuingVirtioNet
{
    /// <summary>Borrowed owner state, never copied or externally mutated while
    /// bound. Slots/tokens contain sixteen entries, RX first. Retain all storage
    /// through actual device reset/bus-master disable/fence and ReleaseAfterReset.
    /// Stopped state is retired; reuse requires a distinct fresh state/epoch.</summary>

    public struct State
    {
        internal ulong Self, Epoch;
        internal byte* Data;
        internal DmaAddressRange Range;
        internal VirtqueueSplitRingPlan Plan;
        internal VirtqueueDescriptorPoolState Rx, Tx;
        internal VirtqueueDescriptorToken* Tokens;
        internal void* Context;
        internal delegate* unmanaged<void*, byte> Fence;
        internal ushort RxAvailable, RxUsed, TxAvailable, TxUsed;
        internal byte TxMask, Phase;
        internal ulong Received, Reclaimed, Dropped;
        public readonly bool Faulted => Phase == 2;
        public readonly bool Retired => Phase == 3;
        public readonly ushort ReceiveAvailableIndex => RxAvailable;
        public readonly ushort ReceiveUsedIndex => RxUsed;
        public readonly ushort TransmitAvailableIndex => TxAvailable;
        public readonly ushort TransmitUsedIndex => TxUsed;
        public readonly ulong ReceivedFrames => Received;
        /// <summary>Unsupported packets whose validated receive buffers were
        /// recycled without publishing payload. Saturation faults before reuse.</summary>

        public readonly ulong DroppedFrames => Dropped;
        public readonly ulong ReclaimedTransmits => Reclaimed;
    }
}
