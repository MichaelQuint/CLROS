using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWriteLogBytesRequest
{
    public readonly uint StreamId;
    public readonly uint Flags;
    public readonly OSHostByteSpan Buffer;
    public readonly ulong Reserved;
    public OSHostWriteLogBytesRequest(uint streamId, uint flags, OSHostByteSpan buffer, ulong reserved)
    {
        StreamId = streamId;
        Flags = flags;
        Buffer = buffer;
        Reserved = reserved;
    }
}
