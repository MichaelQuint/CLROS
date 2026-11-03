namespace OS.Kernel.Platform;

public readonly struct MonotonicClockDeltaResult
{
    public readonly MonotonicClockMathStatus Status;
    public readonly ulong DeltaTicks;
    public readonly ulong AccumulatedTicks;
    public bool Succeeded => Status == MonotonicClockMathStatus.Success;

    internal MonotonicClockDeltaResult(MonotonicClockMathStatus status, ulong delta, ulong accumulated)
    {
        Status = status;
        DeltaTicks = delta;
        AccumulatedTicks = accumulated;
    }
}
