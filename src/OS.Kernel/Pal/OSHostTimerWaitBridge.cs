namespace OS.Pal;

public static class OSHostTimerWaitBridge
{
    public const bool UsesMockTimerTable = true;
    public const bool UsesMockWaitTable = true;
    public const bool SignalsCompletionWaitHandle = true;
    public const bool ExportsPalFunctions = false;
    public const bool BlocksOrParksThreads = false;
    public const bool WakesScheduler = false;
    public const bool ProgramsHardwareTimer = false;
    public const bool HandlesInterrupts = false;
    public const bool StartsKestrel = false;
    public static OSHostTimerWaitBridgeStatus SignalIfDue(OSHostTimerTable? timerTable, OSHostWaitTable? waitTable, OSHostHandle timer, out OSHostTimerWaitBridgeResult result)
    {
        if (timerTable is null)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.MissingTimerTable, OSHostStatus.InvalidArgument, OSHostStatus.InvalidArgument, timer, default, firedCount: 0, nextDueTicks: 0, timerSignaled: false, eventSet: false);
            return result.Status;
        }

        if (waitTable is null)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.MissingWaitTable, OSHostStatus.Ok, OSHostStatus.InvalidArgument, timer, default, firedCount: 0, nextDueTicks: 0, timerSignaled: false, eventSet: false);
            return result.Status;
        }

        OSHostStatus entryStatus = timerTable.TryGetEntry(timer, out OSHostTimerTableEntry entry);
        if (entryStatus != OSHostStatus.Ok)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.TimerEntryFailed, entryStatus, OSHostStatus.InvalidArgument, timer, default, firedCount: 0, nextDueTicks: 0, timerSignaled: false, eventSet: false);
            return result.Status;
        }

        if (!entry.CompletionWaitHandle.IsValid)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.MissingCompletionWaitHandle, OSHostStatus.Ok, OSHostStatus.InvalidArgument, timer, default, firedCount: 0, entry.DueTicks, timerSignaled: false, eventSet: false);
            return result.Status;
        }

        OSHostStatus waitLookupStatus = waitTable.TryGetEntry(entry.CompletionWaitHandle, out OSHostWaitTableEntry waitEntry);
        if (waitLookupStatus != OSHostStatus.Ok || waitEntry.Canceled)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.WaitSetFailed, OSHostStatus.Ok, waitLookupStatus == OSHostStatus.Ok ? OSHostStatus.Interrupted : waitLookupStatus, timer, entry.CompletionWaitHandle, firedCount: 0, entry.DueTicks, timerSignaled: false, eventSet: false);
            return result.Status;
        }

        OSHostTimerPollResult poll = timerTable.Poll(timer);
        if (!poll.Succeeded)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.TimerPollFailed, poll.Status, OSHostStatus.InvalidArgument, timer, entry.CompletionWaitHandle, poll.FiredCount, poll.NextDueTicks, poll.Signaled, eventSet: false);
            return result.Status;
        }

        if (!poll.Signaled)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.TimerNotSignaled, poll.Status, OSHostStatus.WouldBlock, timer, entry.CompletionWaitHandle, poll.FiredCount, poll.NextDueTicks, timerSignaled: false, eventSet: false);
            return result.Status;
        }

        OSHostHandle completionWaitHandle = poll.CompletionWaitHandle.IsValid ? poll.CompletionWaitHandle : entry.CompletionWaitHandle;
        OSHostStatus waitStatus = waitTable.Set(completionWaitHandle);
        if (waitStatus != OSHostStatus.Ok)
        {
            result = CreateResult(OSHostTimerWaitBridgeStatus.WaitSetFailed, poll.Status, waitStatus, timer, completionWaitHandle, poll.FiredCount, poll.NextDueTicks, timerSignaled: true, eventSet: false);
            return result.Status;
        }

        result = CreateResult(OSHostTimerWaitBridgeStatus.Success, poll.Status, waitStatus, timer, completionWaitHandle, poll.FiredCount, poll.NextDueTicks, timerSignaled: true, eventSet: true);
        return result.Status;
    }

    private static OSHostTimerWaitBridgeResult CreateResult(OSHostTimerWaitBridgeStatus status, OSHostStatus timerStatus, OSHostStatus waitStatus, OSHostHandle timer, OSHostHandle completionWaitHandle, ulong firedCount, ulong nextDueTicks, bool timerSignaled, bool eventSet)
    {
        return OSHostTimerWaitBridgeResult.Create(status, timerStatus, waitStatus, timer, completionWaitHandle, firedCount, nextDueTicks, timerSignaled, eventSet);
    }
}
