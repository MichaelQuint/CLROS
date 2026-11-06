using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static partial class NativeAotServiceQuotaRunnerTests
{
    private sealed record Capture(QemuSmokeOptions Options, QemuSmokePlan Plan, ProcessResult Runner, byte[] Raw, byte[] Summary, QemuTranscripts.ServiceRunnerContext Context, byte[] Pcap, uint Anchor);
}
