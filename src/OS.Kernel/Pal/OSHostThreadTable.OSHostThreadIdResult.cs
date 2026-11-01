namespace OS.Pal;
public readonly struct OSHostThreadIdResult
{
    public readonly OSHostStatus Status;
    public readonly ulong ThreadId;
    public readonly bool IsInitialized;
    public OSHostThreadIdResult(OSHostStatus status, ulong threadId, bool isInitialized = true)
    {
        Status = status;
        ThreadId = threadId;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && ThreadId != 0;

    public static OSHostThreadIdResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadIdResult(status, threadId: 0);
    }
}
