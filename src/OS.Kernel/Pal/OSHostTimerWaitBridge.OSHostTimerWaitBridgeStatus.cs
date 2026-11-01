namespace OS.Pal;
public enum OSHostTimerWaitBridgeStatus
{
    Success = 0,
    MissingTimerTable = 1,
    MissingWaitTable = 2,
    TimerEntryFailed = 3,
    MissingCompletionWaitHandle = 4,
    TimerPollFailed = 5,
    TimerNotSignaled = 6,
    WaitSetFailed = 7
}
