using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWaitOneRequest
{
    public readonly OSHostHandle Waitable;
    public readonly OSHostTimeout Timeout;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostWaitOneRequest(OSHostHandle waitable, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Waitable = waitable;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}
