using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalTimerWaitBridgeTests
{

    [HostTest("pal.timer.waitbridge signals completion event", HostLane.Fast, 0)]
    private static bool TestSignalsCompletionEvent()
    {
        OSHostTimerTable timerTable = new(tickFrequency: 1_000, capacity: 1);
        OSHostWaitTable waitTable = new(capacity: 1);
        OSHostHandle waitable = CreateEvent(waitTable);
        OSHostTimerCreateResult timer = timerTable.Create(OneShot(10, waitable));

        OSHostTimerWaitBridgeStatus status =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable,
                waitTable,
                timer.Timer,
                out OSHostTimerWaitBridgeResult result);
        OSHostWaitResult beforeDue = waitTable.WaitOne(waitable);

        timerTable.AdvanceTo(10);
        OSHostTimerWaitBridgeStatus firedStatus =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable,
                waitTable,
                timer.Timer,
                out OSHostTimerWaitBridgeResult fired);
        OSHostWaitResult afterDue = waitTable.WaitOne(waitable);

        return HostCheck.Result(timer.Succeeded
            && status == OSHostTimerWaitBridgeStatus.TimerNotSignaled
            && beforeDue.StatusCode == (int)OSHostStatus.WouldBlock
            && firedStatus == OSHostTimerWaitBridgeStatus.Success
            && fired.Succeeded
            && fired.IsInitialized
            && fired.TimerStatus == OSHostStatus.Ok
            && fired.WaitStatus == OSHostStatus.Ok
            && fired.CompletionWaitHandle.Value == waitable.Value
            && fired.FiredCount == 1
            && fired.TimerSignaled
            && fired.EventSet
            && afterDue.StatusCode == (int)OSHostStatus.Ok
            && afterDue.Kind == OSHostWaitConstants.ResultKindSignaled
            && afterDue.SignaledHandle.Value == waitable.Value);
    }

    [HostTest("pal.timer.waitbridge leaves pending timer unsignaled", HostLane.Fast, 1)]
    private static bool TestLeavesPendingTimerUnsignaled()
    {
        OSHostTimerTable timerTable = new(tickFrequency: 1_000, capacity: 1);
        OSHostWaitTable waitTable = new(capacity: 1);
        OSHostHandle waitable = CreateEvent(waitTable);
        OSHostTimerCreateResult timer = timerTable.Create(OneShot(20, waitable));
        timerTable.AdvanceTo(5);

        OSHostTimerWaitBridgeStatus status =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable,
                waitTable,
                timer.Timer,
                out OSHostTimerWaitBridgeResult result);
        OSHostWaitResult wait = waitTable.WaitOne(waitable);

        return HostCheck.Result(timer.Succeeded
            && status == OSHostTimerWaitBridgeStatus.TimerNotSignaled
            && !result.Succeeded
            && result.IsInitialized
            && result.TimerStatus == OSHostStatus.Ok
            && result.WaitStatus == OSHostStatus.WouldBlock
            && result.FiredCount == 0
            && result.NextDueTicks == 20
            && !result.TimerSignaled
            && !result.EventSet
            && wait.StatusCode == (int)OSHostStatus.WouldBlock
            && timerTable.TryGetEntry(timer.Timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Armed
            && entry.CompletionWaitHandle.Value == waitable.Value);
    }

    [HostTest("pal.timer.waitbridge rejects missing completion handle", HostLane.Fast, 2)]
    private static bool TestRejectsMissingCompletionHandle()
    {
        OSHostTimerTable timerTable = new(tickFrequency: 1_000, capacity: 1);
        OSHostWaitTable waitTable = new(capacity: 1);
        OSHostTimerCreateResult timer = timerTable.Create(OneShot(10, default));
        timerTable.AdvanceTo(10);

        OSHostTimerWaitBridgeStatus status =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable,
                waitTable,
                timer.Timer,
                out OSHostTimerWaitBridgeResult result);

        return HostCheck.Result(timer.Succeeded
            && status == OSHostTimerWaitBridgeStatus.MissingCompletionWaitHandle
            && !result.Succeeded
            && result.IsInitialized
            && result.TimerStatus == OSHostStatus.Ok
            && result.WaitStatus == OSHostStatus.InvalidArgument
            && result.FiredCount == 0
            && !result.EventSet
            && timerTable.TryGetEntry(timer.Timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Armed);
    }

    [HostTest("pal.timer.waitbridge propagates wait set failure", HostLane.Fast, 3)]
    private static bool TestPropagatesWaitSetFailure()
    {
        OSHostTimerTable timerTable = new(tickFrequency: 1_000, capacity: 1);
        OSHostWaitTable sourceWaitTable = new(capacity: 1);
        OSHostWaitTable wrongWaitTable = new(capacity: 1);
        OSHostHandle waitable = CreateEvent(sourceWaitTable);
        OSHostTimerCreateResult timer = timerTable.Create(OneShot(10, waitable));
        timerTable.AdvanceTo(10);

        OSHostTimerWaitBridgeStatus status =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable,
                wrongWaitTable,
                timer.Timer,
                out OSHostTimerWaitBridgeResult result);

        return HostCheck.Result(timer.Succeeded
            && status == OSHostTimerWaitBridgeStatus.WaitSetFailed
            && !result.Succeeded
            && result.IsInitialized
            && result.TimerStatus == OSHostStatus.Ok
            && result.WaitStatus == OSHostStatus.InvalidArgument
            && result.FiredCount == 0
            && !result.TimerSignaled
            && !result.EventSet
            && result.CompletionWaitHandle.Value == waitable.Value
            && timerTable.TryGetEntry(timer.Timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Armed);
    }

    [HostTest("pal.timer.waitbridge preserves mock-only boundary", HostLane.Fast, 4)]
    private static bool TestPreservesMockOnlyBoundary()
    {
        OSHostWaitTable waitTable = new(capacity: 1);
        OSHostTimerWaitBridgeStatus missingTimer =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable: null,
                waitTable,
                new OSHostHandle(1),
                out OSHostTimerWaitBridgeResult result);

        return HostCheck.Result(OSHostTimerWaitBridge.UsesMockTimerTable
            && OSHostTimerWaitBridge.UsesMockWaitTable
            && OSHostTimerWaitBridge.SignalsCompletionWaitHandle
            && !OSHostTimerWaitBridge.ExportsPalFunctions
            && !OSHostTimerWaitBridge.BlocksOrParksThreads
            && !OSHostTimerWaitBridge.WakesScheduler
            && !OSHostTimerWaitBridge.ProgramsHardwareTimer
            && !OSHostTimerWaitBridge.HandlesInterrupts
            && !OSHostTimerWaitBridge.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimerWaitBridgeResult>()
            && missingTimer == OSHostTimerWaitBridgeStatus.MissingTimerTable
            && result.IsInitialized
            && result.TimerStatus == OSHostStatus.InvalidArgument
            && result.WaitStatus == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.timer.waitbridge default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostTimerWaitBridgeResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostTimerWaitBridgeStatus.Success
            && result.TimerStatus == OSHostStatus.Ok
            && result.WaitStatus == OSHostStatus.Ok
            && result.Timer.Value == 0
            && result.CompletionWaitHandle.Value == 0
            && result.FiredCount == 0
            && result.NextDueTicks == 0
            && !result.TimerSignaled
            && !result.EventSet
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimerWaitBridgeResult>());
    }

    private static OSHostHandle CreateEvent(OSHostWaitTable waitTable)
    {
        OSHostEventCreateResult create = waitTable.CreateEvent(
            OSHostWaitableKind.ManualResetEvent,
            initiallySignaled: false);
        if (!create.Succeeded)
        {
            throw new InvalidOperationException("Failed to create wait event fixture.");
        }

        return create.Waitable;
    }

    private static OSHostTimerOptions OneShot(ulong dueTicks, OSHostHandle completionWaitHandle)
    {
        return new OSHostTimerOptions(
            OSHostTimerTable.TimerOptionsVersion,
            flags: 0,
            dueTicks,
            periodTicks: 0,
            completionWaitHandle);
    }
}
