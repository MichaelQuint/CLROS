using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAcceptRequest
{
    public readonly OSHostHandle Listener;
    public readonly OSHostTimeout Timeout;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostSocketAcceptRequest(OSHostHandle listener, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Listener = listener;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}
