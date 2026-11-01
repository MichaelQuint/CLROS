namespace OS.Pal;
public readonly struct OSHostThreadSleepResult
{
    public readonly OSHostStatus Status;
    public readonly uint TimeoutKind;
    public readonly bool CompletedSynchronously;
    public readonly bool IsInitialized;
    public OSHostThreadSleepResult(OSHostStatus status, uint timeoutKind, bool completedSynchronously, bool isInitialized = true)
    {
        Status = status;
        TimeoutKind = timeoutKind;
        CompletedSynchronously = completedSynchronously;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && CompletedSynchronously;

    public static OSHostThreadSleepResult FromStatus(OSHostStatus status, uint timeoutKind)
    {
        return new OSHostThreadSleepResult(status, timeoutKind, completedSynchronously: false);
    }
}
