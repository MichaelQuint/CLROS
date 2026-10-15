namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Finite execution limits for the serialized native thread driver.
/// Policies with at most eight workers retain 128 acknowledged IRQ epochs and
/// 512 supervisor steps. Larger policies allow 8192 epochs and 16384 steps,
/// independently bounded to five minutes of actual monotonic nanoseconds.
/// This matches the broader Linux qualifier's enclosing process time limit;
/// it is a ceiling, not evidence that a guest fits or a calibrated APIC period.
/// The mapped native owner retains this value through its process lifetime.
/// No hardware, clock, scheduler, mapping, completion or cleanup is supplied.</summary>
public struct LinuxThreadExecutionBudget
{
    public const ulong RuntimeNanoseconds = 300_000_000_000;
    private uint _workers;
    private ulong _started, _last;
    public readonly ulong StartedNanoseconds => _started;
    public readonly ulong ObservedNanoseconds => _last;
    public readonly uint MaximumEpochs => _workers == 0 ? 0U : _workers <= 8 ? 128U : 8192U;
    public readonly uint MaximumSteps => _workers == 0 ? 0U : _workers <= 8 ? 512U : 16384U;

    /// <summary>Admit one fresh value for 1..16 workers and a genuine monotonic
    /// clock snapshot (zero for the original policy). Reentry or invalid input
    /// rejects without mutation. This establishes no native execution authority.</summary>
    public bool Begin(uint workers, ulong now)
    {
        if (_workers != 0 || workers == 0 || workers > LinuxThreadProgram.MaximumWorkers || now > long.MaxValue) return false;
        _workers = workers; _started = _last = now; return true;
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
            _workers > 8 && now - _started >= RuntimeNanoseconds) return false;
        _last = now; return true;
    }
}
