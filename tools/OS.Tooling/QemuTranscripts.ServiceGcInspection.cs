namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Value-only joins of the supplied entitlement, kernel budget and
    /// two application GC measurements. Write/call, package, native ownership
    /// and guest execution must be independently verified by the parent replay.
    /// Peak backing includes all dynamic pool use; it does not classify GC
    /// versus native allocations or establish native headroom.</summary>

    public readonly record struct ServiceGcInspection(ulong GcLimitBytes, ulong NativeReserveBytes, ulong StaticBytes, ulong PoolBytes, ulong EntitlementBytes, ulong PeakBackedBytes, ServiceGcMeasurement Begin, ServiceGcMeasurement End, ServiceNativeMeasurement NativeBegin, ServiceNativeMeasurement NativeEnd, string TerminalStatus);
}
