using System.Globalization;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only copied control JSON and checked process observations.
    /// JSON is only structurally bounded here; its detailed controls, packets,
    /// source/native identity and guest cleanup require independent joins.</summary>

    public readonly record struct ServiceRunnerInspection(string ControlsJson, string FramesJson, int ProcessId, DateTimeOffset StartUtc, DateTimeOffset EndUtc, long LifetimeMilliseconds);
}
