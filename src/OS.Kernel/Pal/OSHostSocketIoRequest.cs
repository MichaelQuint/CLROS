using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketIoRequest
{
    public readonly OSHostHandle Socket;
    public readonly OSHostByteSpan Buffer;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostSocketIoRequest(OSHostHandle socket, OSHostByteSpan buffer, uint flags, uint reserved)
    {
        Socket = socket;
        Buffer = buffer;
        Flags = flags;
        Reserved = reserved;
    }
}
