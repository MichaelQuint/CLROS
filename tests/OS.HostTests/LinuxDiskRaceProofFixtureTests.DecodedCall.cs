using System.Buffers.Binary;
using System.Text;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static partial class LinuxDiskRaceProofFixtureTests
{
    internal readonly record struct DecodedCall(ulong Rip, ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5);
}
