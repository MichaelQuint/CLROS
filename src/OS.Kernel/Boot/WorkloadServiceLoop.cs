namespace OS.Boot;
/// <summary>
/// One-use cooperative service pump on one owned CPU. The adapter proves genuine
/// clock frequency, callback/context lifetime, retained mappings and revoked
/// firmware authority. These declarations alone are not hardware evidence.
/// The owned controller requests shutdown on the original state; it is not an
/// application thread, scheduler, interrupt wait, power-off or external admin API.
/// Every wait has a read budget. A consumed failure must not be reset or retried.
/// </summary>

public static unsafe class WorkloadServiceLoop
{
    public const uint MaximumIterations = 64, MaximumPolls = 1000000;
    public static bool RequestShutdown(ref WorkloadServiceLoopState state)
    {
        if (state.StatusValue != WorkloadServiceStatus.Running || state.MinimumValue < 2 || state.IterationsValue < state.MinimumValue)
            return false;
        state.StatusValue = WorkloadServiceStatus.ShutdownRequested;
        return true;
    }

    public static WorkloadServiceStatus Run(ref WorkloadServiceLoopState state, WorkloadServiceLoopRequest request, WorkloadServiceLoopCallbacks callbacks)
    {
        if (state.StatusValue != WorkloadServiceStatus.Uninitialized)
            return WorkloadServiceStatus.AlreadyConsumed;
        if (state.IterationsValue != 0 || state.ReadsValue != 0 || state.MinimumValue != 0 || state.FirstValue != 0 || state.LastValue != 0 || state.IntervalValue != 0 || !request.FirmwareExited || !request.RetainedPointersCleared || !request.OwnedMappingsValidated || request.FrequencyHz < 1000000 || request.FrequencyHz > 10000000000 || request.IntervalMilliseconds == 0 || request.IntervalMilliseconds > 1000 || request.MinimumIterations < 2 || request.MaximumIterations < request.MinimumIterations || request.MaximumIterations > MaximumIterations || request.PollLimit == 0 || request.PollLimit > MaximumPolls || callbacks.Context == null || callbacks.ReadClock == null || callbacks.Service == null || callbacks.Stop == null)
            return WorkloadServiceStatus.InvalidAdmission;
        // Bounds above make the ceiling conversion and total read count exact.
        ulong interval = (request.FrequencyHz * request.IntervalMilliseconds + 999) / 1000;
        state.StatusValue = WorkloadServiceStatus.Running;
        state.MinimumValue = request.MinimumIterations;
        state.IntervalValue = interval;
        ulong now = 0;
        state.ReadsValue++;
        if (callbacks.ReadClock(callbacks.Context, &now) != 1)
            return Fail(ref state, WorkloadServiceStatus.ClockFailed);
        state.FirstValue = state.LastValue = now;
        fixed (WorkloadServiceLoopState* original = &state)
        {
            for (uint iteration = 1; iteration <= request.MaximumIterations; iteration++)
            {
                if (state.LastValue > ~0UL - interval)
                    return Fail(ref state, WorkloadServiceStatus.DeadlineOverflow);
                ulong deadline = state.LastValue + interval;
                bool reached = false;
                for (uint poll = 0; poll < request.PollLimit; poll++)
                {
                    state.ReadsValue++;
                    if (callbacks.ReadClock(callbacks.Context, &now) != 1)
                        return Fail(ref state, WorkloadServiceStatus.ClockFailed);
                    if (now < state.LastValue)
                        return Fail(ref state, WorkloadServiceStatus.ClockRegressed);
                    state.LastValue = now;
                    if (now >= deadline)
                    {
                        reached = true;
                        break;
                    }
                }

                if (!reached)
                    return Fail(ref state, WorkloadServiceStatus.DeadlineExhausted);
                state.IterationsValue = iteration;
                uint reads = state.ReadsValue;
                ulong first = state.FirstValue;
                if (callbacks.Service(callbacks.Context, original, iteration, now) != 1)
                    return Fail(ref state, WorkloadServiceStatus.ServiceFailed);
                if (!SameCounters(state, request.MinimumIterations, iteration, reads, first, now, interval))
                    return Fail(ref state, WorkloadServiceStatus.StateChanged);
                if (state.StatusValue == WorkloadServiceStatus.ShutdownRequested)
                {
                    if (iteration < request.MinimumIterations)
                        return Fail(ref state, WorkloadServiceStatus.StateChanged);
                    if (callbacks.Stop(callbacks.Context, iteration, now) != 1)
                        return Fail(ref state, WorkloadServiceStatus.ShutdownFailed);
                    if (state.StatusValue != WorkloadServiceStatus.ShutdownRequested || !SameCounters(state, request.MinimumIterations, iteration, reads, first, now, interval))
                        return Fail(ref state, WorkloadServiceStatus.StateChanged);
                    state.StatusValue = WorkloadServiceStatus.Stopped;
                    return state.StatusValue;
                }

                if (state.StatusValue != WorkloadServiceStatus.Running)
                    return Fail(ref state, WorkloadServiceStatus.StateChanged);
            }
        }

        return Fail(ref state, WorkloadServiceStatus.ShutdownMissing);
    }

    private static WorkloadServiceStatus Fail(ref WorkloadServiceLoopState state, WorkloadServiceStatus status)
    {
        state.StatusValue = status;
        return status;
    }

    private static bool SameCounters(WorkloadServiceLoopState state, uint minimum, uint iteration, uint reads, ulong first, ulong last, ulong interval) => state.MinimumValue == minimum && state.IterationsValue == iteration && state.ReadsValue == reads && state.FirstValue == first && state.LastValue == last && state.IntervalValue == interval;
}
