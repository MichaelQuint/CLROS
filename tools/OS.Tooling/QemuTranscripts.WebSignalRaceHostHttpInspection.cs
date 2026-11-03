using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Every known request, including polls/retries/incomplete suffixes,
    /// paired to its FIFO response on the same observed stream. Unknown attempted
    /// writes remain separate. Values grant no original I/O or guest authority.</summary>

    public sealed record WebSignalRaceHostHttpInspection(ServiceQuotaHostPrefixes Prefixes, IReadOnlyList<ServiceQuotaHostHttpMessage> Messages, IReadOnlyList<WebSignalRaceHostHttpSample> Samples);
}
