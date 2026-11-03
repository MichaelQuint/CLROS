using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>One parsed actual host request and its FIFO response prefix, if
    /// any. Original stream offsets and intersecting operation IDs remain explicit.
    /// A complete HTTP frame is not an endpoint/lifecycle completion witness.</summary>

    public sealed record ServiceQuotaHostHttpMessage(int ConnectionId, int ExchangeId, ServiceQuotaHttpPrefix Request, ServiceQuotaHttpPrefix? Response, IReadOnlyList<int> RequestOperations, IReadOnlyList<int> ResponseOperations);
}
