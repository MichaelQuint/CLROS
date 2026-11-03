using System.Globalization;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied uniquely assigned payload flows and required HTTP ranges.
    /// Empty guest flows and unassigned host attempts remain explicit, without
    /// invented identity/delivery. All owned serial/native/process joins remain.</summary>

    public sealed record ServiceQuotaStreamJoinInspection(ServiceQuotaHostHttpInspection Host, ServiceQuotaPacketStreams Guest, IReadOnlyList<ServiceQuotaFlowBinding> Bindings, IReadOnlyList<int> UnassignedHostConnections, IReadOnlyList<uint> EmptyGuestPorts, IReadOnlyList<ServiceQuotaHttpPacketInterval> Intervals);
}
