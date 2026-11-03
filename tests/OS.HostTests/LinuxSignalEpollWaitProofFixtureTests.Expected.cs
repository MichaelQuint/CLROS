using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;

internal static unsafe partial class LinuxSignalEpollWaitProofFixtureTests
{
    private readonly record struct Expected(uint Number, ulong A = 0, ulong B = 0, ulong C = 0, ulong D = 0, ulong E = 0, bool Fd = false);
}
