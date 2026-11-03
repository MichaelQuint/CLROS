using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketConnectRequest
{
    public readonly OSHostHandle Socket;
    public readonly OSHostSocketAddress RemoteAddress;
    public readonly OSHostTimeout Timeout;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostSocketConnectRequest(OSHostHandle socket, OSHostSocketAddress remoteAddress, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Socket = socket;
        RemoteAddress = remoteAddress;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}
