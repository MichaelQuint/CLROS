namespace OS.Pal;
public readonly struct OSHostThreadCreateResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostHandle Thread;
    public readonly ulong ThreadId;
    public readonly bool IsInitialized;
    public OSHostThreadCreateResult(OSHostStatus status, OSHostHandle thread, ulong threadId, bool isInitialized = true)
    {
        Status = status;
        Thread = thread;
        ThreadId = threadId;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && Thread.IsValid && ThreadId != 0;

    public static OSHostThreadCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadCreateResult(status, default, threadId: 0);
    }
}
