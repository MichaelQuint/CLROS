using OS.Tooling;
using OS.Manifest;
using OS.Kernel.Compatibility.LinuxX64;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

internal static partial class LinuxDiskParentTests
{
    private delegate byte[] BclPackageBuilder(ReadOnlySpan<byte> elf, string phase, string digest);
}
