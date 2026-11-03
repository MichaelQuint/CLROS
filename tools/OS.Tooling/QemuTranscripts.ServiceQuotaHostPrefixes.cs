namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied byte inventory only. Unknown attempted bytes are counted
    /// separately from known bytes. Empty connections are retained in ID order;
    /// an empty quota-partial trace supplies no completion/execution witness.</summary>

    public sealed record ServiceQuotaHostPrefixes(int KnownReadBytes, int KnownWriteBytes, int UnknownAttemptedWriteBytes, IReadOnlyList<ServiceQuotaHostConnection> Connections);
}
