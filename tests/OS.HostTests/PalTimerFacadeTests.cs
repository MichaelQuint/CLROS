using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Pal;

internal static class PalTimerFacadeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.timer facade returns monotonic snapshot", TestReturnsMonotonicSnapshot, ref failures);
        Run("pal.timer facade creates and arms one shot", TestCreatesAndArmsOneShot, ref failures);
        Run("pal.timer facade arms completion wait handle", TestArmsCompletionWaitHandle, ref failures);
        Run("pal.timer facade cancels and closes handles", TestCancelsAndClosesHandles, ref failures);
        Run("pal.timer facade translates invalid handles", TestTranslatesInvalidHandles, ref failures);
        Run("pal.timer facade reports exhaustion", TestReportsExhaustion, ref failures);
        Run("pal.timer facade rejects null table", TestRejectsNullTable, ref failures);
        Run("pal.timer facade poll output layout is stable", TestPollOutputLayoutIsStable, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL timer facade host tests failed: " + failures);
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

    private static unsafe bool TestReturnsMonotonicSnapshot()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000_000, capacity: 1);
        OSHostTimerFacade facade = new(table);

        if (table.AdvanceTo(125) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimeSnapshot snapshot;
        OSHostStatus status = facade.TimeGetMonotonicTicks(&snapshot);

        return status == OSHostStatus.Ok
            && snapshot.MonotonicTicks == 125
            && snapshot.TickFrequency == 1_000_000
            && snapshot.WallClockUnixNanoseconds == 0
            && facade.TimeGetMonotonicTicks(null) == OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestCreatesAndArmsOneShot()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerFacade facade = new(table);
        OSHostHandle timer;

        if (facade.TimerCreate(&timer) != OSHostStatus.Ok ||
            !timer.IsValid ||
            facade.TimerArm(timer, OneShot(dueTicks: 10)) != OSHostStatus.Ok)
        {
            return false;
        }

        if (table.TryGetEntry(timer, out OSHostTimerTableEntry entry) != OSHostStatus.Ok ||
            entry.State != OSHostTimerLifecycleState.Armed ||
            entry.DueTicks != 10 ||
            entry.Periodic)
        {
            return false;
        }

        if (table.AdvanceTo(9) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollOutput beforeDue;
        if (facade.TimerPoll(timer, &beforeDue) != OSHostStatus.Ok ||
            beforeDue.IsSignaled ||
            beforeDue.FiredCount != 0 ||
            beforeDue.NextDueTicks != 10)
        {
            return false;
        }

        if (table.AdvanceTo(10) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollOutput fired;
        return facade.TimerPoll(timer, &fired) == OSHostStatus.Ok
            && fired.IsSignaled
            && fired.FiredCount == 1
            && fired.NextDueTicks == 0
            && table.TryGetEntry(timer, out OSHostTimerTableEntry disarmed) == OSHostStatus.Ok
            && disarmed.State == OSHostTimerLifecycleState.Created
            && disarmed.TotalFiredCount == 1;
    }

    private static unsafe bool TestCancelsAndClosesHandles()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerFacade facade = new(table);
        OSHostHandle timer;

        if (facade.TimerCreate(&timer) != OSHostStatus.Ok ||
            facade.TimerArm(timer, OneShot(dueTicks: 20)) != OSHostStatus.Ok ||
            facade.TimerCancel(timer) != OSHostStatus.Ok ||
            table.AdvanceTo(100) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostTimerPollOutput poll;
        if (facade.TimerPoll(timer, &poll) != OSHostStatus.Ok ||
            poll.IsSignaled ||
            poll.FiredCount != 0)
        {
            return false;
        }

        return facade.TimerClose(timer) == OSHostStatus.Ok
            && facade.TimerClose(timer) == OSHostStatus.InvalidArgument
            && facade.TimerPoll(timer, &poll) == OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestArmsCompletionWaitHandle()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerFacade facade = new(table);
        OSHostHandle timer;
        OSHostHandle completionWaitHandle = new(0x0000_0001_0000_0001UL);

        if (facade.TimerCreate(&timer) != OSHostStatus.Ok ||
            facade.TimerArm(timer, OneShot(10, completionWaitHandle)) != OSHostStatus.Ok)
        {
            return false;
        }

        return table.TryGetEntry(timer, out OSHostTimerTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostTimerLifecycleState.Armed
            && entry.CompletionWaitHandle.Value == completionWaitHandle.Value;
    }

    private static unsafe bool TestTranslatesInvalidHandles()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerFacade facade = new(table);
        OSHostHandle zero = new(0);
        OSHostHandle missing = new(0x0000_0001_0000_0002UL);
        OSHostTimerPollOutput poll;
        OSHostHandle timer;

        if (facade.TimerArm(zero, OneShot(10)) != OSHostStatus.InvalidArgument ||
            facade.TimerCancel(zero) != OSHostStatus.InvalidArgument ||
            facade.TimerClose(zero) != OSHostStatus.InvalidArgument ||
            facade.TimerPoll(zero, &poll) != OSHostStatus.InvalidArgument ||
            facade.TimerPoll(missing, &poll) != OSHostStatus.InvalidArgument ||
            facade.TimerPoll(missing, null) != OSHostStatus.InvalidArgument ||
            facade.TimerCreate(null) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (facade.TimerCreate(&timer) != OSHostStatus.Ok)
        {
            return false;
        }

        return facade.TimerArm(timer, OneShot(0)) == OSHostStatus.InvalidArgument
            && facade.TimerArm(timer, Periodic(dueTicks: 10, periodTicks: 0)) == OSHostStatus.InvalidArgument
            && facade.TimerArm(timer, new OSHostTimerOptions(
                OSHostTimerTable.TimerOptionsVersion,
                OSHostTimerTable.TimerFlagPeriodic << 1,
                dueTicks: 10,
                periodTicks: 0,
                completionWaitHandle: default)) == OSHostStatus.InvalidArgument;
    }

    private static unsafe bool TestReportsExhaustion()
    {
        OSHostTimerTable table = new(tickFrequency: 1_000, capacity: 1);
        OSHostTimerFacade facade = new(table);
        OSHostHandle first;
        OSHostHandle second = new(0xFFFF);

        if (facade.TimerCreate(&first) != OSHostStatus.Ok ||
            !first.IsValid ||
            facade.TimerCreate(&second) != OSHostStatus.OutOfMemory ||
            second.IsValid)
        {
            return false;
        }

        OSHostHandle reused;
        return facade.TimerClose(first) == OSHostStatus.Ok
            && facade.TimerCreate(&reused) == OSHostStatus.Ok
            && reused.IsValid
            && reused.Value != first.Value;
    }

    private static unsafe bool TestRejectsNullTable()
    {
        OSHostTimerFacade facade = new(table: null);
        OSHostTimeSnapshot snapshot;
        OSHostHandle timer;
        OSHostTimerPollOutput poll;
        OSHostHandle invalid = new(1);

        return facade.TimeGetMonotonicTicks(&snapshot) == OSHostStatus.InvalidArgument
            && facade.TimerCreate(&timer) == OSHostStatus.InvalidArgument
            && facade.TimerArm(invalid, OneShot(10)) == OSHostStatus.InvalidArgument
            && facade.TimerCancel(invalid) == OSHostStatus.InvalidArgument
            && facade.TimerClose(invalid) == OSHostStatus.InvalidArgument
            && facade.TimerPoll(invalid, &poll) == OSHostStatus.InvalidArgument;
    }

    private static bool TestPollOutputLayoutIsStable()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<OSHostTimerPollOutput>())
        {
            return false;
        }

        return Marshal.SizeOf<OSHostTimerPollOutput>() == 24
            && Offset<OSHostTimerPollOutput>(nameof(OSHostTimerPollOutput.FiredCount)) == 0
            && Offset<OSHostTimerPollOutput>(nameof(OSHostTimerPollOutput.NextDueTicks)) == 8
            && Offset<OSHostTimerPollOutput>(nameof(OSHostTimerPollOutput.Signaled)) == 16
            && Offset<OSHostTimerPollOutput>(nameof(OSHostTimerPollOutput.Reserved)) == 20;
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

    private static int Offset<T>(string fieldName)
    {
        return (int)Marshal.OffsetOf<T>(fieldName);
    }
}
