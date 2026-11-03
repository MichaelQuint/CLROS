namespace OS.Pal;

public readonly struct OSHostTimerCreateResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostHandle Timer;
    public OSHostTimerCreateResult(OSHostStatus status, OSHostHandle timer, bool isInitialized = true)
    {
        Status = status;
        Timer = timer;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostTimerCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostTimerCreateResult(status, default);
    }
}
