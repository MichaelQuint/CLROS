using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied selected sample binding. A host cancellation names its
    /// related logical exchange and MessageIndex=-1; it is never an HTTP response.
    /// Other samples name one complete FIFO request/response message.</summary>

    public readonly record struct WebSignalRaceHostHttpSample(int Sample, int ExchangeId, int MessageIndex, bool HostCancellation);
}
