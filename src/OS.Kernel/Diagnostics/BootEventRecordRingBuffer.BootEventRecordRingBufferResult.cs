namespace OS.Kernel.Diagnostics;
public readonly struct BootEventRecordRingBufferResult
{
    private BootEventRecordRingBufferResult(bool isInitialized, BootEventRecordRingBufferStatus status, int written, int required, int count, ulong nextSequence)
    {
        IsInitialized = isInitialized;
        Status = status;
        Written = written;
        Required = required;
        Count = count;
        NextSequence = nextSequence;
    }

    public bool IsInitialized { get; }
    public BootEventRecordRingBufferStatus Status { get; }
    public int Written { get; }
    public int Required { get; }
    public int Count { get; }
    public ulong NextSequence { get; }
    public bool Succeeded => IsInitialized && Status == BootEventRecordRingBufferStatus.Success;

    public static BootEventRecordRingBufferResult Success(int written, int required, int count, ulong nextSequence)
    {
        return new BootEventRecordRingBufferResult(true, BootEventRecordRingBufferStatus.Success, written, required, count, nextSequence);
    }

    public static BootEventRecordRingBufferResult Fail(BootEventRecordRingBufferStatus status, int written, int required, int count, ulong nextSequence)
    {
        return new BootEventRecordRingBufferResult(true, status, written, required, count, nextSequence);
    }
}
