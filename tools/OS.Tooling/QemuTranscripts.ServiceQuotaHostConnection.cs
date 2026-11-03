namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Both directions for the original session-local connection ID.
    /// No host endpoint, guest tuple, flow, sample or clock identity is inferred.</summary>

    public sealed record ServiceQuotaHostConnection(int ConnectionId, ServiceQuotaHostDirection Reads, ServiceQuotaHostDirection Writes);
}
