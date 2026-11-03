using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostThreadCreateOptions
{
    public readonly uint Version;
    public readonly uint Flags;
    public readonly ulong StackSizeBytes;
    public readonly ulong GuardSizeBytes;
    public readonly OSHostByteSpan NameUtf8;
    public readonly OSHostThreadStart Start;
    public OSHostThreadCreateOptions(uint version, uint flags, ulong stackSizeBytes, ulong guardSizeBytes, OSHostByteSpan nameUtf8, OSHostThreadStart start)
    {
        Version = version;
        Flags = flags;
        StackSizeBytes = stackSizeBytes;
        GuardSizeBytes = guardSizeBytes;
        NameUtf8 = nameUtf8;
        Start = start;
    }
}
