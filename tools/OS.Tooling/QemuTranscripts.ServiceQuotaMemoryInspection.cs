using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Quota-only values with no invented positive drain/recovery or
    /// GC_END measurement. NativeReserveBytes is declared shared-pool headroom,
    /// not an enforced partition. GcArena describes live backing at denial;
    /// end0 is independently required from the owner's real cleanup summary.
    /// MaximumNonGcUpperBoundBytes excludes only still-owned GC arena backing;
    /// GC bookkeeping elsewhere remains nonGC conservatively. Added limit/static
    /// bounds may exceed the shared pool or entitlement and confer no budget
    /// fit, native partition or stack/socket peak proof. Snapshot storage is
    /// an immutable copied view, never a borrowed array. RetainedMappings/Bytes
    /// count joined virtual mmap spans, never inferred physical allocations.</summary>

    public readonly record struct ServiceQuotaMemoryInspection(ulong GcLimitBytes, ulong NativeReserveBytes, ulong StaticBytes, ulong PoolBytes, ulong EntitlementBytes, ulong PeakBackedBytes, ServiceGcMeasurement StartupGc, ServiceGcBackingInspection GcArena, IReadOnlyList<ServiceQuotaMemorySnapshot> Snapshots, int PressureSnapshots, ulong PressurePeakBackedBytes, ulong RetainedMappings, ulong RetainedBytes, ServiceQuotaMemorySnapshot Denial, ulong MaximumNonGcUpperBoundBytes, ulong GcLimitPlusNonGcUpperBoundBytes, ulong StaticPlusGcLimitAndNonGcUpperBoundBytes)
    {
        /// <summary>Copied unique retained mmap positions in original normalized
        /// rows. Existing full ownership/mutation checks precede projection;
        /// virtual spans are not independent physical-allocation evidence.</summary>

        public IReadOnlyList<ServiceQuotaRetainedMapping> RetainedCalls { get; init; } = Array.Empty<ServiceQuotaRetainedMapping>();
    }
}
