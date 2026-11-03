using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied host prefixes, every parsed request (including unselected
    /// polls/retries), and selected sample bindings. Known bytes exclude unknown
    /// failed-write attempts. These values authenticate no original I/O or guest.</summary>

    public sealed record ServiceQuotaHostHttpInspection(ServiceQuotaHostPrefixes Prefixes, IReadOnlyList<ServiceQuotaHostHttpMessage> Messages, IReadOnlyList<ServiceQuotaHostHttpSample> Samples);
}
