namespace OS.Pal;

public readonly struct OSHostTimerPollResult
{
    public readonly OSHostStatus Status;
    public readonly ulong FiredCount;
    public readonly ulong NextDueTicks;
    public readonly OSHostHandle CompletionWaitHandle;
    public readonly bool Signaled;
    public OSHostTimerPollResult(OSHostStatus status, ulong firedCount, ulong nextDueTicks, OSHostHandle completionWaitHandle, bool signaled, bool isInitialized = true)
    {
        Status = status;
        FiredCount = firedCount;
        NextDueTicks = nextDueTicks;
        CompletionWaitHandle = completionWaitHandle;
        Signaled = signaled;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostTimerPollResult FromStatus(OSHostStatus status)
    {
        return new OSHostTimerPollResult(status, 0, 0, default, signaled: false);
    }
}
