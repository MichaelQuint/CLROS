namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>One selected guest-observed port; no host endpoint association
    /// or lifetime deduplication is made. Requests are client-to-guest direction.</summary>

    public sealed record ServiceQuotaPacketStream(uint RemotePort, ServiceQuotaPacketDirection Requests, ServiceQuotaPacketDirection Responses, ServiceQuotaPacketFacts Facts);
}
