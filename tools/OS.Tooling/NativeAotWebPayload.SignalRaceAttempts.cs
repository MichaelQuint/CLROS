using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private const int MaximumRaceAttempts = 1024;
    private static string RaceCaptureFailure(Exception error) => error is InvalidDataException && error.Message.StartsWith("web_signalrace_", StringComparison.Ordinal) ? error.Message : error.GetType().Name;
}
