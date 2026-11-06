namespace OS.Pal;

public readonly struct OSHostRandomBytesResult
{
    public readonly OSHostStatus Status;
    public readonly uint BytesWritten;
    public readonly uint RequestSequence;
    public readonly bool IsInitialized;
    public OSHostRandomBytesResult(OSHostStatus status, uint bytesWritten, uint requestSequence, bool isInitialized = true)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequestSequence = requestSequence;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && BytesWritten != 0;

    public static OSHostRandomBytesResult FromStatus(OSHostStatus status)
    {
        return new OSHostRandomBytesResult(status, bytesWritten: 0, requestSequence: uint.MaxValue);
    }
}
