using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalTimerWaitBridgeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.timer.waitbridge signals completion event", TestSignalsCompletionEvent, ref failures);
        Run("pal.timer.waitbridge leaves pending timer unsignaled", TestLeavesPendingTimerUnsignaled, ref failures);
        Run("pal.timer.waitbridge rejects missing completion handle", TestRejectsMissingCompletionHandle, ref failures);
        Run("pal.timer.waitbridge propagates wait set failure", TestPropagatesWaitSetFailure, ref failures);
        Run("pal.timer.waitbridge preserves mock-only boundary", TestPreservesMockOnlyBoundary, ref failures);
        Run("pal.timer.waitbridge default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL timer wait bridge host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

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

        return timer.Succeeded
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
            && afterDue.SignaledHandle.Value == waitable.Value;
    }

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

        return timer.Succeeded
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
            && entry.CompletionWaitHandle.Value == waitable.Value;
    }

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

        return timer.Succeeded
            && status == OSHostTimerWaitBridgeStatus.MissingCompletionWaitHandle
            && !result.Succeeded
            && result.IsInitialized
            && result.TimerStatus == OSHostStatus.Ok
            && result.WaitStatus == OSHostStatus.InvalidArgument
            && result.FiredCount == 0
            && !result.EventSet
            && timerTable.TryGetEntry(timer.Timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Armed;
    }

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

        return timer.Succeeded
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
            && entry.State == OSHostTimerLifecycleState.Armed;
    }

    private static bool TestPreservesMockOnlyBoundary()
    {
        OSHostWaitTable waitTable = new(capacity: 1);
        OSHostTimerWaitBridgeStatus missingTimer =
            OSHostTimerWaitBridge.SignalIfDue(
                timerTable: null,
                waitTable,
                new OSHostHandle(1),
                out OSHostTimerWaitBridgeResult result);

        return OSHostTimerWaitBridge.UsesMockTimerTable
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
            && result.WaitStatus == OSHostStatus.InvalidArgument;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostTimerWaitBridgeResult result = default;

        return !result.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimerWaitBridgeResult>();
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
