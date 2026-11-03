using System.Globalization;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static partial class LinuxDiskRaceTranscriptTests
{
    private sealed record Pending(F.CallSpec Spec, ulong Sequence, ulong Request, uint Source);
}
