using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketShutdownRequest
{
    public readonly OSHostHandle Socket;
    public readonly uint Direction;
    public readonly uint Flags;
    public readonly ulong Reserved;
    public OSHostSocketShutdownRequest(OSHostHandle socket, uint direction, uint flags, ulong reserved)
    {
        Socket = socket;
        Direction = direction;
        Flags = flags;
        Reserved = reserved;
    }
}
