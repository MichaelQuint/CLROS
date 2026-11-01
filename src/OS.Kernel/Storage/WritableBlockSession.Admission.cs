using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    /// <summary>Borrowed authority after successful VERSION_1+FLUSH negotiation,
    /// status11, firmware DMA quiescence and MSI-X disabled. RO must not be
    /// offered. Epoch is unique for this zero-state lifetime; pool/state/slots,
    /// ordinary DMA and MMIO remain exclusive and disjoint with no hidden aliases.
    /// Timeout bounds one request and is never renewed by Poll. IRQ callbacks
    /// must remain null. This receipt alone grants no hardware authority.</summary>
    public struct Admission
    {
        public VirtioMappedRegion Common, Notify, Device;
        public uint NotifyMultiplier, ResetLimit;
        public DmaBufferPool* Pool;
        public ulong Epoch, ExpectedCapacity, CompletionTimeoutNanos;
        public VirtioNegotiationResult Negotiation;
        public VirtioBlkReadAccess Access;
        public ClockAccess Clock;
        /// <summary>Optional exclusive aligned slot, disjoint from owner,
        /// pool/slots, DMA, MMIO and every source/output. Borrowed until Stop;
        /// only valid admitted Start clears it. Null preserves legacy behavior.</summary>
        public Observation* Observation;
    }
}
