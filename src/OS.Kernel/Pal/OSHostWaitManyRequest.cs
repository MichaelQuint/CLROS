using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWaitManyRequest
{
    public readonly OSHostByteSpan Waitables;
    public readonly OSHostTimeout Timeout;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostWaitManyRequest(OSHostByteSpan waitables, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Waitables = waitables;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}
