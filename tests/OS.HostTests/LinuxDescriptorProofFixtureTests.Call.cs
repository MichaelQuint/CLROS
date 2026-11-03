using System.Buffers.Binary;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe partial class LinuxDescriptorProofFixtureTests
{
    private readonly record struct Call(uint Number, uint A0, uint A1, uint A2, uint A3, ulong Rip);
}
