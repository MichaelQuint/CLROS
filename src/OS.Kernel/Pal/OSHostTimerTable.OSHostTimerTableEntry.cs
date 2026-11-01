namespace OS.Pal;
public readonly struct OSHostTimerTableEntry
{
    public readonly OSHostHandle Timer;
    public readonly OSHostTimerLifecycleState State;
    public readonly ulong DueTicks;
    public readonly ulong PeriodTicks;
    public readonly ulong TotalFiredCount;
    public readonly OSHostHandle CompletionWaitHandle;
    public readonly bool Periodic;
    public OSHostTimerTableEntry(OSHostHandle timer, OSHostTimerLifecycleState state, ulong dueTicks, ulong periodTicks, ulong totalFiredCount, OSHostHandle completionWaitHandle, bool periodic)
    {
        Timer = timer;
        State = state;
        DueTicks = dueTicks;
        PeriodTicks = periodTicks;
        TotalFiredCount = totalFiredCount;
        CompletionWaitHandle = completionWaitHandle;
        Periodic = periodic;
    }
}
