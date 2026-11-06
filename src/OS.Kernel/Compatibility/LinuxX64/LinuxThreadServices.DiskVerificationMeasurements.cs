#if !NOSTDLIB_EFI || M10_018_TIMING_PROBE
namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Diagnostic values for serialized, nonnested verification calls
    /// wholly inside one armed/frozen phase. The owner supplies monotonic clock
    /// samples and retains this zeroed value through Freeze. Rejection is sticky;
    /// it invalidates measurement, never the verification being observed. No
    /// callback, pointer, allocation, per-call record or execution authority is
    /// retained. Elapsed time includes measurement boundary overhead.</summary>

    public struct DiskVerificationMeasurements
    {
        private ulong _lastTime, _phaseStart, _started, _calls, _elapsed;
        private byte _phase, _active, _failed;
        public bool Arm(ulong now)
        {
            if (_phase != 0 || _active != 0 || _failed != 0)
                return Reject();
            _phaseStart = _lastTime = now;
            _phase = 1;
            return true;
        }

        public bool Begin(ulong now)
        {
            if (_phase != 1 || _active != 0 || !Observe(now))
                return Reject();
            _started = now;
            _active = 1;
            return true;
        }

        public bool End(ulong now)
        {
            if (_phase != 1 || _active != 1 || !Observe(now))
                return Reject();
            ulong elapsed = now - _started;
            if (_calls == ulong.MaxValue || _elapsed > ulong.MaxValue - elapsed || _elapsed + elapsed > now - _phaseStart)
                return Reject();
            _elapsed += elapsed;
            _calls++;
            _active = 0;
            return true;
        }

        public bool Freeze(ulong now)
        {
            if (_phase != 1 || _active != 0 || !Observe(now) || _elapsed > now - _phaseStart)
                return Reject();
            _phase = 2;
            return true;
        }

        /// <summary>A failed external clock/owner/phase sample invalidates the
        /// observation. The caller must still execute the original verifier.</summary>

        public void Invalidate() => _failed = 1;
        /// <summary>Copy only complete frozen count/time totals. An incomplete
        /// or invalid value returns false and clears both outputs.</summary>

        public readonly bool Inspect(out ulong calls, out ulong nanoseconds)
        {
            bool complete = _phase == 2 && _failed == 0;
            calls = complete ? _calls : 0;
            nanoseconds = complete ? _elapsed : 0;
            return complete;
        }

        private bool Observe(ulong now)
        {
            if (_failed != 0 || now < _lastTime)
                return Reject();
            _lastTime = now;
            return true;
        }

        private bool Reject()
        {
            _failed = 1;
            return false;
        }
    }
}
#endif
