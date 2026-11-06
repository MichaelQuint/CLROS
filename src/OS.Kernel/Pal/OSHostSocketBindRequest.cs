using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketBindRequest
{
    public readonly OSHostHandle Socket;
    public readonly OSHostSocketAddress Address;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostSocketBindRequest(OSHostHandle socket, OSHostSocketAddress address, uint flags, uint reserved)
    {
        Socket = socket;
        Address = address;
        Flags = flags;
        Reserved = reserved;
    }
}
