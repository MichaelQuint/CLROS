namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied HTTP-to-syscall values only. Totals count consuming
    /// receives/accepted sends, excluding peeks; they are not packet delivery,
    /// physical allocation, quota-overlap, cleanup or guest acceptance facts.</summary>

    public sealed record ServiceQuotaTemporalInspection(int SocketObservations, ulong RequestBytesConsumed, ulong ResponseBytesAccepted, IReadOnlyList<ServiceQuotaHttpSyscallInterval> Intervals);
}
