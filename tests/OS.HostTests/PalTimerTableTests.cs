using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalTimerTableTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.timer monotonic snapshot is stable", TestMonotonicSnapshotIsStable, ref failures);
        Run("pal.timer one shot arms and fires", TestOneShotArmsAndFires, ref failures);
        Run("pal.timer stores completion wait handle", TestStoresCompletionWaitHandle, ref failures);
        Run("pal.timer cancel prevents fire", TestCancelPreventsFire, ref failures);
        Run("pal.timer periodic advances deterministically", TestPeriodicAdvancesDeterministically, ref failures);
        Run("pal.timer timeout deadline comparison is deterministic", TestTimeoutDeadlineComparisonIsDeterministic, ref failures);
        Run("pal.timer invalid handles are rejected", TestInvalidHandlesAreRejected, ref failures);
        Run("pal.timer default results are not success", TestDefaultResultsAreNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL timer table host tests failed: " + failures);
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

    private static bool TestMonotonicSnapshotIsStable()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000_000, capacity: 2);
        OSHostTimeSnapshot initial = table.GetSnapshot(wallClockUnixNanoseconds: 123);

        if (initial.MonotonicTicks != 0 ||
            initial.TickFrequency != 1_000_000 ||
            initial.WallClockUnixNanoseconds != 123)
        {
            return false;
        }

        if (table.AdvanceBy(250) != OSHostStatus.Ok ||
            table.AdvanceTo(500) != OSHostStatus.Ok ||
            table.AdvanceTo(499) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        OSHostTimeSnapshot afterAdvance = table.GetSnapshot();
        return afterAdvance.MonotonicTicks == 500
            && afterAdvance.TickFrequency == 1_000_000
            && table.CurrentTicks == 500
            && table.ActiveCount == 0;
    }

    private static bool TestOneShotArmsAndFires()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerCreateResult create = table.Create();
        if (!create.Succeeded ||
            !create.Timer.IsValid ||
            table.ActiveCount != 1)
        {
            return false;
        }

        if (table.Arm(create.Timer, OneShot(dueTicks: 10)) != OSHostStatus.Ok ||
            table.TryGetEntry(create.Timer, out OSHostTimerTableEntry armed) != OSHostStatus.Ok ||
            armed.State != OSHostTimerLifecycleState.Armed ||
            armed.DueTicks != 10 ||
            armed.Periodic)
        {
            return false;
        }

        if (table.AdvanceTo(9) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollResult beforeDue = table.Poll(create.Timer);
        if (!beforeDue.Succeeded || beforeDue.Signaled || beforeDue.NextDueTicks != 10)
        {
            return false;
        }

        if (table.AdvanceTo(10) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollResult fired = table.Poll(create.Timer);
        OSHostTimerPollResult secondPoll = table.Poll(create.Timer);
        return fired.Succeeded
            && fired.Signaled
            && fired.FiredCount == 1
            && fired.NextDueTicks == 0
            && secondPoll.Succeeded
            && !secondPoll.Signaled
            && table.TryGetEntry(create.Timer, out OSHostTimerTableEntry disarmed) == OSHostStatus.Ok
            && disarmed.State == OSHostTimerLifecycleState.Created
            && disarmed.TotalFiredCount == 1;
    }

    private static bool TestCancelPreventsFire()
    {
        OSHostTimerTable table = new(tickFrequency: 10_000, capacity: 1);
        OSHostTimerCreateResult create = table.Create(OneShot(dueTicks: 20));
        if (!create.Succeeded ||
            table.Cancel(create.Timer) != OSHostStatus.Ok ||
            table.AdvanceTo(100) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollResult poll = table.Poll(create.Timer);
        return poll.Succeeded
            && !poll.Signaled
            && poll.FiredCount == 0
            && table.TryGetEntry(create.Timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Created
            && entry.TotalFiredCount == 0;
    }

    private static bool TestStoresCompletionWaitHandle()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostHandle completionWaitHandle = new(0x0000_0001_0000_0001UL);
        OSHostTimerCreateResult create = table.Create(OneShot(10, completionWaitHandle));
        if (!create.Succeeded ||
            table.TryGetEntry(create.Timer, out OSHostTimerTableEntry entry) != OSHostStatus.Ok ||
            entry.CompletionWaitHandle.Value != completionWaitHandle.Value)
        {
            return false;
        }

        OSHostTimerPollResult beforeDue = table.Poll(create.Timer);
        if (!beforeDue.Succeeded ||
            beforeDue.Signaled ||
            beforeDue.CompletionWaitHandle.Value != completionWaitHandle.Value)
        {
            return false;
        }

        if (table.AdvanceTo(10) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollResult fired = table.Poll(create.Timer);
        return fired.Succeeded
            && fired.Signaled
            && fired.CompletionWaitHandle.Value == completionWaitHandle.Value
            && table.TryGetEntry(create.Timer, out OSHostTimerTableEntry disarmed) == OSHostStatus.Ok
            && !disarmed.CompletionWaitHandle.IsValid;
    }

    private static bool TestPeriodicAdvancesDeterministically()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerCreateResult create = table.Create(Periodic(dueTicks: 10, periodTicks: 5));
        if (!create.Succeeded)
        {
            return false;
        }

        if (table.AdvanceTo(24) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollResult firstPoll = table.Poll(create.Timer);
        if (!firstPoll.Succeeded ||
            !firstPoll.Signaled ||
            firstPoll.FiredCount != 3 ||
            firstPoll.NextDueTicks != 25)
        {
            return false;
        }

        OSHostTimerPollResult beforeNext = table.Poll(create.Timer);
        if (!beforeNext.Succeeded || beforeNext.Signaled || beforeNext.NextDueTicks != 25)
        {
            return false;
        }

        if (table.AdvanceBy(11) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollResult secondPoll = table.Poll(create.Timer);
        return secondPoll.Succeeded
            && secondPoll.Signaled
            && secondPoll.FiredCount == 3
            && secondPoll.NextDueTicks == 40
            && table.TryGetEntry(create.Timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Armed
            && entry.Periodic
            && entry.TotalFiredCount == 6;
    }

    private static bool TestTimeoutDeadlineComparisonIsDeterministic()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        if (table.AdvanceTo(100) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimeoutEvaluationResult infinite = table.EvaluateTimeout(OSHostSocketTimeouts.Infinite);
        OSHostTimeoutEvaluationResult immediate = table.EvaluateTimeout(OSHostSocketTimeouts.Immediate);
        OSHostTimeoutEvaluationResult absolutePending = table.EvaluateTimeout(
            Absolute(deadlineTicks: 120, tickFrequency: table.TickFrequency));
        OSHostTimeoutEvaluationResult relativePending = table.EvaluateTimeout(
            Relative(relativeTicks: 25, tickFrequency: table.TickFrequency));
        OSHostTimeoutEvaluationResult mismatchedFrequency = table.EvaluateTimeout(
            Absolute(deadlineTicks: 120, tickFrequency: table.TickFrequency + 1));

        if (!infinite.Succeeded ||
            infinite.State != OSHostTimeoutState.Infinite ||
            infinite.DeadlineTicks != 0 ||
            infinite.RemainingTicks != ulong.MaxValue ||
            !immediate.Succeeded ||
            !immediate.IsExpired ||
            immediate.DeadlineTicks != 100 ||
            immediate.RemainingTicks != 0 ||
            !absolutePending.Succeeded ||
            absolutePending.State != OSHostTimeoutState.Pending ||
            absolutePending.DeadlineTicks != 120 ||
            absolutePending.RemainingTicks != 20 ||
            !relativePending.Succeeded ||
            relativePending.State != OSHostTimeoutState.Pending ||
            relativePending.DeadlineTicks != 125 ||
            relativePending.RemainingTicks != 25 ||
            mismatchedFrequency.Status != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (table.AdvanceTo(120) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimeoutEvaluationResult absoluteExpired = table.EvaluateTimeout(
            Absolute(deadlineTicks: 120, tickFrequency: table.TickFrequency));
        OSHostTimeoutEvaluationResult zeroRelative = table.EvaluateTimeout(
            Relative(relativeTicks: 0, tickFrequency: table.TickFrequency));

        return absoluteExpired.Succeeded
            && absoluteExpired.IsExpired
            && absoluteExpired.DeadlineTicks == 120
            && absoluteExpired.RemainingTicks == 0
            && zeroRelative.Succeeded
            && zeroRelative.IsExpired
            && zeroRelative.DeadlineTicks == 120
            && table.EvaluateTimeout(new OSHostTimeout(99, 0, 0, 0)).Status == OSHostStatus.InvalidArgument;
    }

    private static bool TestInvalidHandlesAreRejected()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostHandle zero = new(0);
        OSHostHandle missing = new(0x0000_0001_0000_0002UL);

        if (table.Arm(zero, OneShot(10)) != OSHostStatus.InvalidArgument ||
            table.Cancel(zero) != OSHostStatus.InvalidArgument ||
            table.Close(zero) != OSHostStatus.InvalidArgument ||
            table.Poll(zero).Status != OSHostStatus.InvalidArgument ||
            table.Poll(missing).Status != OSHostStatus.InvalidArgument ||
            table.Create(OneShot(0)).Status != OSHostStatus.InvalidArgument ||
            table.Create(Periodic(10, 0)).Status != OSHostStatus.InvalidArgument ||
            table.Create(new OSHostTimerOptions(
                OSHostTimerTable.TimerOptionsVersion,
                OSHostTimerTable.TimerFlagPeriodic << 1,
                dueTicks: 10,
                periodTicks: 0,
                completionWaitHandle: default)).Status != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        OSHostTimerCreateResult first = table.Create(OneShot(10));
        if (!first.Succeeded ||
            table.Create().Status != OSHostStatus.OutOfMemory ||
            table.Close(first.Timer) != OSHostStatus.Ok ||
            table.Poll(first.Timer).Status != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        OSHostTimerCreateResult reused = table.Create(OneShot(30));
        return reused.Succeeded
            && reused.Timer.Value != first.Timer.Value
            && table.Poll(first.Timer).Status == OSHostStatus.InvalidArgument
            && table.Poll(reused.Timer).Status == OSHostStatus.Ok;
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostTimeoutEvaluationResult timeout = default;
        OSHostTimerCreateResult create = default;
        OSHostTimerPollResult poll = default;

        return !timeout.IsInitialized
            && !timeout.Succeeded
            && timeout.Status == OSHostStatus.Ok
            && timeout.State == OSHostTimeoutState.Infinite
            && timeout.DeadlineTicks == 0
            && timeout.RemainingTicks == 0
            && !create.IsInitialized
            && !create.Succeeded
            && create.Status == OSHostStatus.Ok
            && create.Timer.Value == 0
            && !poll.IsInitialized
            && !poll.Succeeded
            && poll.Status == OSHostStatus.Ok
            && poll.FiredCount == 0
            && poll.NextDueTicks == 0
            && poll.CompletionWaitHandle.Value == 0
            && !poll.Signaled
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimeoutEvaluationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimerCreateResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimerPollResult>();
    }

    private static OSHostTimerOptions OneShot(ulong dueTicks)
    {
        return new OSHostTimerOptions(
            OSHostTimerTable.TimerOptionsVersion,
            flags: 0,
            dueTicks,
            periodTicks: 0,
            completionWaitHandle: default);
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

    private static OSHostTimerOptions Periodic(ulong dueTicks, ulong periodTicks)
    {
        return new OSHostTimerOptions(
            OSHostTimerTable.TimerOptionsVersion,
            OSHostTimerTable.TimerFlagPeriodic,
            dueTicks,
            periodTicks,
            completionWaitHandle: default);
    }

    private static OSHostTimeout Absolute(ulong deadlineTicks, ulong tickFrequency)
    {
        return new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindAbsoluteTicks,
            flags: 0,
            deadlineTicks,
            tickFrequency);
    }

    private static OSHostTimeout Relative(ulong relativeTicks, ulong tickFrequency)
    {
        return new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindRelativeTicks,
            flags: 0,
            relativeTicks,
            tickFrequency);
    }
}
