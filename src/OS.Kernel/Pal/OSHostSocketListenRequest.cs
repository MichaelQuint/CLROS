using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketListenRequest
{
    public readonly OSHostHandle Socket;
    public readonly uint Backlog;
    public readonly uint Flags;
    public readonly ulong Reserved;
    public OSHostSocketListenRequest(OSHostHandle socket, uint backlog, uint flags, ulong reserved)
    {
        Socket = socket;
        Backlog = backlog;
        Flags = flags;
        Reserved = reserved;
    }
}
