using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Required actual HTTP bytes joined to every original first range.
    /// Kind is complete-request/complete-response/cancel-header/echo-prefix/
    /// pressure-request. A header/partial body witness is not endpoint completion.</summary>

    public sealed record ServiceQuotaHttpPacketInterval(int ExchangeId, int ConnectionId, uint RemotePort, bool Response, string Kind, int Start, int End, IReadOnlyList<ServiceQuotaHttpPacketRange> Ranges);
}
