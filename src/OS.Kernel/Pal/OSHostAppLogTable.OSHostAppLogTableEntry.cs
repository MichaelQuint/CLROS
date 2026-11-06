using OS.Std.Memory;

namespace OS.Pal;

public readonly struct OSHostAppLogTableEntry
{
    public readonly uint StreamId;
    public readonly uint Flags;
    public readonly uint Sequence;
    public readonly uint ByteLength;
    public OSHostAppLogTableEntry(uint streamId, uint flags, uint sequence, uint byteLength)
    {
        StreamId = streamId;
        Flags = flags;
        Sequence = sequence;
        ByteLength = byteLength;
    }

    public bool IsStdout => StreamId == OSHostAppLogConstants.StreamStdout;
    public bool IsStderr => StreamId == OSHostAppLogConstants.StreamStderr;
}
