using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Borrowed host UTC interval around one complete HTTP probe.
    /// Production joins it to the owned process/runner receipt; caller values
    /// alone establish neither trusted time nor guest execution.</summary>

    public readonly record struct WebUtcObservation(DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc);
}
