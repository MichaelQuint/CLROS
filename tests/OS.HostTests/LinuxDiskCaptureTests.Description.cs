using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskFileProofFixture;

internal static partial class LinuxDiskCaptureTests
{
    private sealed class Description(string path)
    {
        internal readonly string Path = path;
        internal ulong Position;
    }
}
