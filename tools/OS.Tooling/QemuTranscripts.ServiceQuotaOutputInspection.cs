using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied application values only. These authenticate neither the
    /// corresponding mmap/GC calls nor peer overlap, native policy or cleanup.</summary>

    public readonly record struct ServiceQuotaOutputInspection(ServiceGcMeasurement StartupGc, ulong CompletedChunks, ulong Attempts, ulong RetainedBytes, ulong ForcedCollections, ulong LastCommittedBytes);
}
