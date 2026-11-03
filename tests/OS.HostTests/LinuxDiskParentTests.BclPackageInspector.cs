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
    private delegate NativeAotLogUploadContract.ClrosPackageValues BclPackageInspector(ReadOnlySpan<byte> package, string phase, string digest);
}
