namespace OS.Pal;
public readonly struct OSHostTimeoutEvaluationResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostTimeoutState State;
    public readonly ulong DeadlineTicks;
    public readonly ulong RemainingTicks;
    public OSHostTimeoutEvaluationResult(OSHostStatus status, OSHostTimeoutState state, ulong deadlineTicks, ulong remainingTicks, bool isInitialized = true)
    {
        Status = status;
        State = state;
        DeadlineTicks = deadlineTicks;
        RemainingTicks = remainingTicks;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;
    public bool IsExpired => State == OSHostTimeoutState.Expired;

    public static OSHostTimeoutEvaluationResult FromStatus(OSHostStatus status)
    {
        return new OSHostTimeoutEvaluationResult(status, OSHostTimeoutState.Expired, deadlineTicks: 0, remainingTicks: 0);
    }
}
