using OS.Std.Memory;

namespace OS.Pal;

public readonly struct OSHostAppLogReadResult
{
    public readonly OSHostStatus Status;
    public readonly uint StreamId;
    public readonly uint Flags;
    public readonly uint Sequence;
    public readonly uint BytesCopied;
    public readonly uint RequiredBytes;
    public readonly bool IsInitialized;
    public OSHostAppLogReadResult(OSHostStatus status, uint streamId, uint flags, uint sequence, uint bytesCopied, uint requiredBytes, bool isInitialized = true)
    {
        Status = status;
        StreamId = streamId;
        Flags = flags;
        Sequence = sequence;
        BytesCopied = bytesCopied;
        RequiredBytes = requiredBytes;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostAppLogReadResult FromStatus(OSHostStatus status)
    {
        return new OSHostAppLogReadResult(status, streamId: 0, flags: 0, sequence: 0, bytesCopied: 0, requiredBytes: 0);
    }
}
