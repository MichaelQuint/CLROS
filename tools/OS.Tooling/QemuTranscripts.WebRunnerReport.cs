using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Joined captured child lifetime and HTTP elapsed observations.
    /// The original ProcessResult, source/native and image qualification remain
    /// mandatory; this report supplies no guest exit or resource release.</summary>

    public readonly record struct WebRunnerReport(int ProcessId, long LifetimeMilliseconds, int HealthMilliseconds, int ConfigurationMilliseconds, IReadOnlyList<WebUtcObservation> UtcObservations, WebControlInspection? Controls);
}
