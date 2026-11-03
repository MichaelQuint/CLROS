using System.Text;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static partial class NativeAotServiceRunnerTests
{
    private sealed record Capture(QemuSmokeOptions Options, QemuSmokePlan Plan, ProcessResult Runner, byte[] Raw, byte[] Summary, QemuTranscripts.ServiceRunnerContext Context);
}
