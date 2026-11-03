using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Exact reconstructed response hash and a comparison hash replacing
    /// only its canonical29-byte Date value with the fixed Unix-epoch text.
    /// Entity/status/header checks still apply; Date plausibility requires the
    /// separate UTC-window join. No bytes are removed from the raw evidence.</summary>

    public readonly record struct WebResponseObservation(DateTimeOffset DateUtc, string RawSha256, string DateNormalizedSha256);
}
