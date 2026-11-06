using OS.Kernel.Compatibility.LinuxX64;

internal static class LinuxThreadExecutionBudgetTests
{
    [HostTest("m16-014 continuing policy requires explicit web admission and measured progress beyond300 seconds")]
    private static void ContinuingService()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(!budget.Begin(1, 100, continuingService: true));
        HostCheck.True(!budget.Begin(1, 100, application: true, continuingService: true));
        HostCheck.True(budget.Begin(1, 100, application: true, continuingWeb: true, continuingService: true));
        HostCheck.True(budget.IsContinuingService && budget.RequiresClock && !budget.HasElapsedLimit);
        for (ulong progress = 1; progress <= 61; progress++)
        {
            ulong now = 100 + progress * 10_000_000_000;
            HostCheck.True(budget.ObserveProgress(now, progress, runnable: true));
            HostCheck.True(budget.Continue(now, 1_000_001 + progress, 4_000_001 + (uint)progress));
        }
        HostCheck.Equal(610_000_000_100UL, budget.ObservedNanoseconds);
        HostCheck.True(!budget.Continue(budget.ObservedNanoseconds, uint.MaxValue, 1));
        HostCheck.True(!budget.Continue(budget.ObservedNanoseconds, 1, uint.MaxValue));
        HostCheck.True(!budget.ObserveProgress(budget.ObservedNanoseconds, 60, true));
    }

    [HostTest("m16-014 runnable watchdog rejects late progress while genuine idle resumes with a fresh deadline")]
    private static void ServiceWatchdog()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(budget.Begin(1, 100, true, true, true));
        HostCheck.True(budget.Continue(30_000_000_099, 1, 1));
        HostCheck.True(!budget.Continue(30_000_000_100, 1, 1));
        HostCheck.True(!budget.ObserveProgress(30_000_000_100, 1, true));
        HostCheck.Equal(30_000_000_099UL, budget.ObservedNanoseconds);
        budget = default;
        HostCheck.True(budget.Begin(1, 100, true, true, true));
        HostCheck.True(budget.ObserveProgress(100, 0, false));
        HostCheck.True(budget.Observe(3_600_000_000_100));
        HostCheck.True(budget.ObserveProgress(3_600_000_000_100, 0, true));
        HostCheck.True(budget.Continue(3_630_000_000_099, 1, 1));
        HostCheck.True(!budget.Continue(3_630_000_000_100, 1, 1));
    }

    [HostTest("m16-014 cancellation latches operational stop and preserves finite qualification behavior")]
    private static void ServiceCancellation()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(budget.Begin(1, 100, true, true));
        HostCheck.True(!budget.RequestCancellation());
        HostCheck.True(!budget.Continue(300_000_000_100, 1, 1));
        budget = default;
        HostCheck.True(budget.Begin(1, 100, true, true, true));
        HostCheck.True(budget.RequestCancellation() && budget.CancellationRequested);
        HostCheck.True(!budget.RequestCancellation() && !budget.Observe(101) && !budget.Continue(101, 1, 1));
        HostCheck.True(!budget.ObserveProgress(101, 1, false));
        HostCheck.Equal(100UL, budget.ObservedNanoseconds);
    }

    [HostTest("m16-014 one busy worker cannot borrow another worker's progress watchdog")]
    private static void WorkerProgressIsolation()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(budget.Begin(2, 100, true, true, true));
        HostCheck.True(budget.ObserveWorker(100, 0, 0, true));
        HostCheck.True(budget.ObserveWorker(100, 1, 0, true));
        HostCheck.True(budget.ObserveProgress(25_000_000_100, 1, true));
        HostCheck.True(budget.ObserveWorker(25_000_000_100, 1, 1, true));
        HostCheck.True(!budget.ObserveWorker(30_000_000_100, 0, 1, true));
        HostCheck.Equal(25_000_000_100UL, budget.ObservedNanoseconds);
        HostCheck.True(budget.ObserveWorker(30_000_000_100, 1, 2, true));
        HostCheck.True(!budget.ObserveWorker(30_000_000_100, 1, 1, true));
        HostCheck.True(!budget.ObserveWorker(30_000_000_100, 2, 0, true));
    }

    [HostTest("m16-014 parked worker re-arms and only reaped generation reset permits counter reuse")]
    private static void WorkerIdleAndReap()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(budget.Begin(1, 100, true, true, true));
        HostCheck.True(budget.ObserveWorker(100, 0, 9, true));
        HostCheck.True(budget.ObserveWorker(200, 0, 9, false));
        HostCheck.True(budget.ObserveWorker(3_600_000_000_100, 0, 9, true));
        HostCheck.True(!budget.ObserveWorker(3_600_000_000_100, 0, 0, true));
        HostCheck.True(budget.ForgetWorker(0));
        HostCheck.True(budget.ObserveWorker(3_600_000_000_100, 0, 0, true));
        HostCheck.True(budget.RequestCancellation() && budget.ForgetWorker(0));
        HostCheck.True(!budget.ObserveWorker(3_600_000_000_100, 0, 1, false));
        HostCheck.True(!budget.ForgetWorker(1));
    }

    [HostTest("linux.thread-budget rejects uninitialized invalid and repeated admission without consuming a lifetime")]
    private static void Admission()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(!budget.Continue(0, 0, 0));
        HostCheck.Equal(0U, budget.MaximumEpochs);
        HostCheck.Equal(0U, budget.MaximumSteps);
        HostCheck.True(!budget.Begin(0, 0) && !budget.Begin(17, 0) && !budget.Begin(16, ulong.MaxValue));
        HostCheck.True(budget.Begin(16, 100));
        HostCheck.True(!budget.Begin(1, 0));
        HostCheck.Equal(8192U, budget.MaximumEpochs);
        HostCheck.Equal(16384U, budget.MaximumSteps);
        HostCheck.True(!budget.Continue(99, 0, 0) && budget.Continue(100, 0, 0));
    }

    [HostTest("linux.thread-budget preserves focused policy count limits independently of APIC count or elapsed time")]
    private static void Focused()
    {
        foreach (uint workers in new uint[] { 1, 4, 8 })
        {
            LinuxThreadExecutionBudget budget = default;
            HostCheck.True(budget.Begin(workers, 0));
            HostCheck.Equal(128U, budget.MaximumEpochs);
            HostCheck.Equal(512U, budget.MaximumSteps);
            HostCheck.True(budget.Continue(0, 127, 511));
            HostCheck.True(!budget.Continue(0, 128, 0) && !budget.Continue(0, 0, 512));
            HostCheck.True(budget.Continue(long.MaxValue, 127, 511));
        }
    }

    [HostTest("linux.thread-budget runtime policy admits real long waits past old limits but independently caps epochs steps and actual time")]
    private static void Runtime()
    {
        foreach (uint workers in new uint[] { 9, 16 })
        {
            LinuxThreadExecutionBudget budget = default;
            const ulong start = 1234567;
            HostCheck.True(budget.Begin(workers, start));
            HostCheck.True(budget.Continue(start + 1_000_000_000, 128, 512)); // Actual one-second wait, not 128 converted ticks.
            HostCheck.True(budget.Continue(start + 299_999_999_999, 8191, 16383));
            HostCheck.True(!budget.Continue(start + 299_999_999_999, 8192, 1));
            HostCheck.True(!budget.Continue(start + 299_999_999_999, 1, 16384));
            HostCheck.True(!budget.Continue(start + 300_000_000_000, 1, 1));
            HostCheck.True(!budget.Continue(start + 300_000_000_001, 1, 1));
            HostCheck.True(budget.Continue(start + 299_999_999_999, 1, 1)); // Rejection did not advance the stored clock.
            HostCheck.True(!budget.Continue(start, 1, 1));
        }
    }

    [HostTest("linux.thread-budget application limits preserve actual small capacity and independent finite ceilings")]
    private static void SmallApplication()
    {
        foreach (uint workers in new uint[] { 1, 4, 8 })
        {
            LinuxThreadExecutionBudget budget = default;
            HostCheck.True(budget.Begin(workers, 100, application: true) && budget.HasElapsedLimit);
            HostCheck.Equal(8192U, budget.MaximumEpochs);
            HostCheck.Equal(16384U, budget.MaximumSteps);
            HostCheck.True(budget.Continue(101, 128, 512));
            HostCheck.True(!budget.Continue(99, 1, 1) && !budget.Continue(101, 8192, 1) &&
                !budget.Continue(101, 1, 16384) && !budget.Observe(300_000_000_100));
            HostCheck.Equal(101UL, budget.ObservedNanoseconds);
            HostCheck.True(budget.Observe(300_000_000_099));
        }
    }

    [HostTest("linux.thread-budget near-end clocks never overflow a deadline or accept invalid time")]
    private static void Arithmetic()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(budget.Begin(16, long.MaxValue - 10));
        HostCheck.True(budget.Continue(long.MaxValue, 1, 1));
        HostCheck.True(!budget.Continue((ulong)long.MaxValue + 1, 1, 1) && !budget.Continue(ulong.MaxValue, 1, 1));
        HostCheck.True(budget.Continue(long.MaxValue, 1, 1));
    }

    [HostTest("linux.thread-budget continuing web admission preserves real time ceiling and independent bounded counters")]
    private static void ContinuingWeb()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(!budget.Begin(16, 100, continuingWeb: true));
        HostCheck.Equal(0u, budget.MaximumSteps);
        HostCheck.Equal(0u, budget.MaximumEpochs);
        HostCheck.True(budget.Begin(16, 100, application: true, continuingWeb: true) && budget.HasElapsedLimit);
        HostCheck.True(budget.Continue(101, 8192, 16384));
        HostCheck.True(budget.Continue(300_000_000_099, LinuxThreadExecutionBudget.WebMaximumEpochs - 1,
            LinuxThreadExecutionBudget.WebMaximumSteps - 1));
        HostCheck.True(!budget.Continue(300_000_000_099, LinuxThreadExecutionBudget.WebMaximumEpochs, 1));
        HostCheck.True(!budget.Continue(300_000_000_099, 1, LinuxThreadExecutionBudget.WebMaximumSteps));
        HostCheck.True(!budget.Continue(300_000_000_100, 1, 1) && !budget.Observe(99));
        HostCheck.Equal(300_000_000_099UL, budget.ObservedNanoseconds);
        HostCheck.True(!budget.Begin(1, 0, application: true, continuingWeb: true));
    }

    [HostTest("linux.thread-budget final completion observes real time without requiring another arm or consuming an expired clock")]
    private static void Completion()
    {
        LinuxThreadExecutionBudget budget = default;
        HostCheck.True(budget.Begin(16, 123));
        HostCheck.Equal(123UL, budget.StartedNanoseconds);
        HostCheck.Equal(123UL, budget.ObservedNanoseconds);
        HostCheck.True(budget.Continue(1000, 8191, 16383));
        HostCheck.True(budget.Observe(300_000_000_122));
        HostCheck.True(!budget.Observe(300_000_000_123));
        HostCheck.Equal(300_000_000_122UL, budget.ObservedNanoseconds);
        HostCheck.True(!default(LinuxThreadExecutionBudget).Observe(0));
    }
}
