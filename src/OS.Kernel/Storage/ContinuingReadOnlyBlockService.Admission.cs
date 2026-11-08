using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Borrowed authority supplied after VERSION_1 and optional RO-only
    /// negotiation, status11 and MSI-X disabled. Epoch must be unique for this
    /// zero-state lifetime. ExpectedCapacity is the admitted nonzero sector count.
    /// Pool/state/metadata and mappings are stable, exclusive, disjoint and have
    /// no hidden physical aliases. Timeout bounds one request, not idle lifetime.
    /// All three interrupt callbacks in Access must remain null.</summary>
    public struct Admission
    {
        public VirtioMappedRegion Common, Notify, Device;
        public uint NotifyMultiplier, ResetLimit;
        public DmaBufferPool* Pool;
        public ulong Epoch, NegotiatedFeatures, ExpectedCapacity, CompletionTimeoutNanos;
        public VirtioBlkReadAccess Access;
        public ClockAccess Clock;
        public ObservationAccess Observation;
        /// <summary>Explicit readonly three-page DMA/16-sector mode. False
        /// retains the legacy one-page/four-sector contract. Pool mapping and
        /// slot metadata must cover the complete selected allocation.</summary>
        public bool UseLargeReadLayout;
    }
}
