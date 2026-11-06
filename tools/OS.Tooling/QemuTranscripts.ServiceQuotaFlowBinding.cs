using System.Globalization;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One unique byte-based assignment inside supplied inventories.
    /// Compared counts and unknown-tail minimum retain their limited meaning;
    /// IDs/ports do not establish original process/file or cross-clock authority.</summary>

    public readonly record struct ServiceQuotaFlowBinding(int ConnectionId, uint RemotePort, ServiceQuotaStreamPairInspection Compared);
}
