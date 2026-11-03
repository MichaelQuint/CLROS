using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe partial class LinuxThreadProofFixtureTests
{
    private readonly record struct Call(uint Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, int FdOffset, ulong Rip);
}
