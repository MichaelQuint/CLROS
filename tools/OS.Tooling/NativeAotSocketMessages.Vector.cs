using System.Globalization;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class NativeAotSocketMessages
{
    /// <summary>One snapshotted user iovec; its address confers no access.</summary>

    public readonly record struct Vector(ulong Address, ulong Bytes);
}
