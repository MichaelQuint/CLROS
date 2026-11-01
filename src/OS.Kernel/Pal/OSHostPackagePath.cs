using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostPackagePath
{
    public readonly OSHostByteSpan PathUtf8;
    public readonly ulong Flags;
    public OSHostPackagePath(OSHostByteSpan pathUtf8, ulong flags)
    {
        PathUtf8 = pathUtf8;
        Flags = flags;
    }
}
