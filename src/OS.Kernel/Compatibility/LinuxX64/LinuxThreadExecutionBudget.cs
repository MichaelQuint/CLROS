namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Finite execution limits for the serialized native thread driver.
/// Mechanical policies with at most eight workers retain 128 acknowledged IRQ
/// epochs and 512 supervisor steps. Explicit application policies and larger
/// worker policies allow 8192 epochs and 16384 steps,
/// independently bounded to five minutes of actual monotonic nanoseconds.
/// Explicit continuing web admission retains that same elapsed ceiling with
/// larger bounded counters; it never infers application completion or cleanup.
/// This matches the broader Linux qualifier's enclosing process time limit;
/// it is a ceiling, not evidence that a guest fits or a calibrated APIC period.
/// The mapped native owner retains this value through its process lifetime.
/// A separately admitted continuing-service policy uses per-turn syscall quotas,
/// actual preemption and a progress watchdog instead of that elapsed ceiling.
/// No hardware, clock, scheduler, mapping, completion or cleanup is supplied.</summary>
public struct LinuxThreadExecutionBudget
{
    public const ulong RuntimeNanoseconds = 300_000_000_000;
    public const uint WebMaximumEpochs = 1_000_000, WebMaximumSteps = 4_000_000;
    public const ulong ServiceProgressNanoseconds = 30_000_000_000;
    private uint _workers;
    private bool _application, _web;
    private bool _service, _cancelled, _runnable;
    private ulong _started, _last;
    private ulong _lastProgress, _progress;
    public readonly ulong StartedNanoseconds => _started;
    public readonly ulong ObservedNanoseconds => _last;
    public readonly bool HasElapsedLimit => _workers != 0 && _application && !_service;
    public readonly bool RequiresClock => _workers != 0 && _application;
    public readonly bool IsContinuingService => _workers != 0 && _service;
    public readonly bool CancellationRequested => _cancelled;
    public readonly uint MaximumEpochs => _workers == 0 ? 0U : _service ? uint.MaxValue : _web ? WebMaximumEpochs : _application ? 8192U : 128U;
    public readonly uint MaximumSteps => _workers == 0 ? 0U : _service ? uint.MaxValue : _web ? WebMaximumSteps : _application ? 16384U : 512U;

    /// <summary>Admit one fresh value for 1..16 workers and a genuine monotonic
    /// clock snapshot (zero for the mechanical policy). The serialized owner
    /// explicitly admits application execution independently of worker capacity.
    /// Continuing web mode requires explicit application admission and keeps
    /// the same real elapsed watchdog. Service mode additionally requires web
    /// admission, genuine preemption/per-turn quotas and ObserveProgress before
    /// every supervisor turn. Its uint counter ceilings reject exhaustion rather
    /// than wrapping. The enclosing source-sealed profile authorizes that mode;
    /// this value cannot infer it from a workload. Reentry or invalid input
    /// rejects without mutation. This establishes no native execution authority.</summary>
    public bool Begin(uint workers, ulong now, bool application = false, bool continuingWeb = false, bool continuingService = false)
    {
        if (_workers != 0 || workers == 0 || workers > LinuxThreadProgram.MaximumWorkers || now > long.MaxValue ||
            continuingWeb && !application || continuingService && (!application || !continuingWeb)) return false;
        _workers = workers; _application = application || workers > 8; _web = continuingWeb;
        _service = continuingService; _runnable = continuingService;
        _started = _last = _lastProgress = now; return true;
    }

    /// <summary>Admit the next supervisor step, before arming another timer or
    /// transferring to a worker. Counts are already committed observations,
    /// never durations. Larger policies require actual nonregressing time.
    /// Failure changes nothing. The caller must still acknowledge/restore any
    /// previously armed interrupt and drain real continuations after expiry;
    /// this check must never be used to reject that acknowledgement or drain.</summary>
    public bool Continue(ulong now, ulong epochs, uint step)
    {
        if (_workers == 0 || epochs >= MaximumEpochs || step >= MaximumSteps) return false;
        return Observe(now);
    }

    /// <summary>Check genuine elapsed time, including after the final worker
    /// reaps when no further timer may be armed. Failure changes nothing; this
    /// does not acknowledge hardware or authorize resource release.</summary>
    public bool Observe(ulong now)
    {
        if (_workers == 0 || _cancelled || now > long.MaxValue || now < _last ||
            HasElapsedLimit && now - _started >= RuntimeNanoseconds ||
            _service && _runnable && now - _lastProgress >= ServiceProgressNanoseconds) return false;
        _last = now; return true;
    }

    /// <summary>Report actual completed syscall/wait/exit progress before each
    /// continuing supervisor turn, under its IRQ-off serialized authority.
    /// The cumulative counter must not regress. Runnable work gets at most30s
    /// without a new observation; timer ticks alone are not worker progress.
    /// An idle process may wait indefinitely, but the first newly runnable turn
    /// starts a fresh watchdog. Late progress cannot forgive an expired runnable
    /// watchdog. Rejection changes nothing. This reports no hardware authority.</summary>
    public bool ObserveProgress(ulong now, ulong completed, bool runnable)
    {
        if (!_service || _workers == 0 || _cancelled || now > long.MaxValue || now < _last || completed < _progress ||
            _runnable && now - _lastProgress >= ServiceProgressNanoseconds) return false;
        if (completed > _progress || !_runnable || !runnable) _lastProgress = now;
        _progress = completed; _runnable = runnable; _last = now; return true;
    }

    /// <summary>Latch one explicit service cancellation. Further operational
    /// admission stops; the native owner must still acknowledge IRQs and drain
    /// real worker continuations before releasing resources. This never turns
    /// a timeout/cancellation into application exit or cleanup evidence.</summary>
    public bool RequestCancellation()
    {
        if (!_service || _workers == 0 || _cancelled) return false;
        _cancelled = true; return true;
    }
}
