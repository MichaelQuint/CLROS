using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe partial class LinuxThreadProofFixtureTests
{
    private sealed record Decoded(List<Call> Calls, List<(int At, int Target)> Branches, HashSet<int> Memory, List<int> Instructions);
}
