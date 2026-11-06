using OS.Std.Memory;

namespace OS.Pal;

public readonly struct OSHostAppLogWriteResult
{
    public readonly OSHostStatus Status;
    public readonly uint RecordIndex;
    public readonly uint BytesWritten;
    public readonly bool IsInitialized;
    public OSHostAppLogWriteResult(OSHostStatus status, uint recordIndex, uint bytesWritten, bool isInitialized = true)
    {
        Status = status;
        RecordIndex = recordIndex;
        BytesWritten = bytesWritten;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostAppLogWriteResult FromStatus(OSHostStatus status)
    {
        return new OSHostAppLogWriteResult(status, uint.MaxValue, bytesWritten: 0);
    }
}
