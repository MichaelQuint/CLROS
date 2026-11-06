namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied known successful directional bytes and all original
    /// operation ranges, including zero/EOF/error events that add no known bytes.
    /// UnknownWrite exists only for a failed write direction, never a failed read.
    /// Base64 strings/lists are immutable; supplied values grant no I/O authority.</summary>

    public sealed record ServiceQuotaHostDirection(int KnownBytes, string KnownBase64, IReadOnlyList<ServiceQuotaHostRange> Operations, ServiceQuotaHostWriteAttempt? UnknownWrite);
}
