using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostRandomBytesRequest
{
    public readonly OSHostByteSpan Destination;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostRandomBytesRequest(OSHostByteSpan destination, uint flags, uint reserved)
    {
        Destination = destination;
        Flags = flags;
        Reserved = reserved;
    }
}
