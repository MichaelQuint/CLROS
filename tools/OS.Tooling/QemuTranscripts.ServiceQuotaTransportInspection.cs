namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied observed history and deliberate abort counts. Retired in
    /// History counts only real expiry; discarded compact records are separate.
    /// These values authenticate no NIC, buffers, packets, native code or guest.</summary>

    public readonly record struct ServiceQuotaTransportInspection(ServiceTransportInspection History, ulong BulkAborted, ulong CompactDiscarded, ulong AbortClock)
    {
        /// <summary>Every validated fresh transport reservation in supplied
        /// normalized row order; copied/read-only, including repeated ports.</summary>

        public IReadOnlyList<ServiceQuotaTransportLifetime> FreshLifetimes { get; init; } = Array.Empty<ServiceQuotaTransportLifetime>();
    }
}
