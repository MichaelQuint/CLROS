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
/// No hardware, clock, scheduler, mapping, completion or cleanup is supplied.</summary>
public struct LinuxThreadExecutionBudget
{
    public const ulong RuntimeNanoseconds = 300_000_000_000;
    public const uint WebMaximumEpochs = 1_000_000, WebMaximumSteps = 4_000_000;
    private uint _workers;
    private bool _application, _web;
    private ulong _started, _last;
    public readonly ulong StartedNanoseconds => _started;
    public readonly ulong ObservedNanoseconds => _last;
    public readonly bool HasElapsedLimit => _workers != 0 && _application;
    public readonly uint MaximumEpochs => _workers == 0 ? 0U : _web ? WebMaximumEpochs : _application ? 8192U : 128U;
    public readonly uint MaximumSteps => _workers == 0 ? 0U : _web ? WebMaximumSteps : _application ? 16384U : 512U;

    /// <summary>Admit one fresh value for 1..16 workers and a genuine monotonic
    /// clock snapshot (zero for the mechanical policy). The serialized owner
    /// explicitly admits application execution independently of worker capacity.
    /// Continuing web mode requires explicit application admission and keeps
    /// the same real elapsed watchdog. Reentry or invalid input
    /// rejects without mutation. This establishes no native execution authority.</summary>
    public bool Begin(uint workers, ulong now, bool application = false, bool continuingWeb = false)
    {
        if (_workers != 0 || workers == 0 || workers > LinuxThreadProgram.MaximumWorkers || now > long.MaxValue ||
            continuingWeb && !application) return false;
        _workers = workers; _application = application || workers > 8; _web = continuingWeb; _started = _last = now; return true;
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
        if (_workers == 0 || now > long.MaxValue || now < _last ||
            _application && now - _started >= RuntimeNanoseconds) return false;
        _last = now; return true;
    }
}
