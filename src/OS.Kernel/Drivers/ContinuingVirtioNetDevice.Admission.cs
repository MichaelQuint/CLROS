using RingEngine = global::OS.Drivers.ContinuingVirtioNet;

namespace OS.Drivers;

public static unsafe partial class ContinuingVirtioNetDevice
{
    /// <summary>Borrowed admission, not mapping or device proof. Supply16 empty
    /// descriptor slots/tokens and zero stable state outside the DMA window and
    /// pool metadata. Epoch is unique for this lifetime. CompletionTimeoutNanos
    /// bounds each outstanding TX independently, not the lifetime of an idle NIC.
    /// Pool pages must be coherent WB/RW/NX; MMIO regions must be owned UC/RW/NX.</summary>

    public struct Admission
    {
        public VirtioMappedRegion Common, Notify;
        public uint NotifyMultiplier, SlotCount, TokenCount, ResetLimit;
        public DmaBufferPool* Pool;
        public VirtqueueDescriptorSlot* Slots;
        public VirtqueueDescriptorToken* Tokens;
        public ulong Epoch, NegotiatedFeatures, CompletionTimeoutNanos;
        public VirtqueuePollingAccess Mmio;
        public ClockAccess Clock;
    }
}
