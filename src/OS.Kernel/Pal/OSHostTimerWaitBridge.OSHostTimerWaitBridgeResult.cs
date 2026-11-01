namespace OS.Pal;
public readonly struct OSHostTimerWaitBridgeResult
{
    private OSHostTimerWaitBridgeResult(OSHostTimerWaitBridgeStatus status, OSHostStatus timerStatus, OSHostStatus waitStatus, OSHostHandle timer, OSHostHandle completionWaitHandle, ulong firedCount, ulong nextDueTicks, bool timerSignaled, bool eventSet, bool isInitialized)
    {
        Status = status;
        TimerStatus = timerStatus;
        WaitStatus = waitStatus;
        Timer = timer;
        CompletionWaitHandle = completionWaitHandle;
        FiredCount = firedCount;
        NextDueTicks = nextDueTicks;
        TimerSignaled = timerSignaled;
        EventSet = eventSet;
        IsInitialized = isInitialized;
    }

    public OSHostTimerWaitBridgeStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == OSHostTimerWaitBridgeStatus.Success;
    public OSHostStatus TimerStatus { get; }
    public OSHostStatus WaitStatus { get; }
    public OSHostHandle Timer { get; }
    public OSHostHandle CompletionWaitHandle { get; }
    public ulong FiredCount { get; }
    public ulong NextDueTicks { get; }
    public bool TimerSignaled { get; }
    public bool EventSet { get; }

    public static OSHostTimerWaitBridgeResult Create(OSHostTimerWaitBridgeStatus status, OSHostStatus timerStatus, OSHostStatus waitStatus, OSHostHandle timer, OSHostHandle completionWaitHandle, ulong firedCount, ulong nextDueTicks, bool timerSignaled, bool eventSet)
    {
        return new OSHostTimerWaitBridgeResult(status, timerStatus, waitStatus, timer, completionWaitHandle, firedCount, nextDueTicks, timerSignaled, eventSet, isInitialized: true);
    }
}
