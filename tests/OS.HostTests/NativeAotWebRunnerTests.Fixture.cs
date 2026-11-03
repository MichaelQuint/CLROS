using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static partial class NativeAotWebRunnerTests
{
    private sealed record Fixture(QemuSmokeOptions Options, QemuSmokePlan Plan, ProcessResult Runner, byte[] Raw, byte[] Summary, QemuTranscripts.WebRunnerContext Context);
}
