#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe partial struct LinuxThreadServices
{
    /// <summary>Diagnostic arithmetic only: one serialized zeroed value follows
    /// real outer dispatch entry/return and an explicitly armed/frozen window.
    /// Times and cumulative poll totals come from the owner; they authorize no
    /// skipped check, callback, scheduling, I/O or user copy. A rejected event
    /// permanently invalidates this value. Freeze clips a currently active
    /// dispatch at that observed boundary; its later physical return cannot
    /// change the frozen totals. Retain the value until that return. No pointer,
    /// clock callback, allocation or per-event output is retained or performed.</summary>

    public struct DiskDispatchMeasurements
    {
        private ulong _lastTime, _lastPoll, _phaseStart, _phasePoll;
        private ulong _segmentStart, _segmentPoll, _dispatch, _nested, _completed, _clipped;
        private ulong _elapsed, _poll, _nextSequence, _activeSequence;
        private byte _phase, _active, _clippedStart, _failed, _pollActive, _physicalAtFreeze;
        public readonly bool IsComplete => _phase == 2 && _failed == 0;
        public readonly bool IsPhysicalDispatchActive => _active != 0;

        public bool BeginDispatch(ulong now, ulong cumulativePoll, out ulong sequence)
        {
            sequence = 0;
            if (_active != 0 || _pollActive != 0 || _phase == 2 || _nextSequence == ulong.MaxValue || !Observe(now, cumulativePoll))
                return Reject();
            _active = 1;
            _clippedStart = 0;
            _activeSequence = ++_nextSequence;
            sequence = _activeSequence;
            if (_phase == 1)
            {
                _segmentStart = now;
                _segmentPoll = cumulativePoll;
            }

            return true;
        }

        public bool Arm(ulong now, ulong cumulativePoll)
        {
            if (_phase != 0 || _pollActive != 0 || !Observe(now, cumulativePoll))
                return Reject();
            _phase = 1;
            _phaseStart = now;
            _phasePoll = cumulativePoll;
            if (_active != 0)
            {
                _segmentStart = now;
                _segmentPoll = cumulativePoll;
                _clippedStart = 1;
            }

            return true;
        }

        public bool EndDispatch(ulong sequence, ulong now, ulong cumulativePoll)
        {
            if (_active != 1 || _pollActive != 0 || sequence == 0 || sequence != _activeSequence || _phase == 2 && cumulativePoll != _lastPoll || !Observe(now, cumulativePoll))
                return Reject();
            if (_phase == 1 && !AddSegment(now, cumulativePoll, _clippedStart != 0))
                return false;
            _active = 0;
            _clippedStart = 0;
            _activeSequence = 0;
            return true;
        }

        public bool Freeze(ulong now, ulong cumulativePoll)
        {
            if (_phase != 1 || _pollActive != 0 || !Observe(now, cumulativePoll) || now < _phaseStart)
                return Reject();
            if (_active != 0 && !AddSegment(now, cumulativePoll, true))
                return false;
            ulong elapsed = now - _phaseStart, poll = cumulativePoll - _phasePoll;
            if (poll > elapsed || _nested > poll || _nested > _dispatch || _dispatch - _nested > elapsed - poll)
                return Reject();
            _elapsed = elapsed;
            _poll = poll;
            _physicalAtFreeze = _active;
            _phase = 2;
            return true;
        }

        // v5 publishes cumulative poll duration only at its successful return.
        // Track its actual entry even before Arm; neither boundary may silently
        // omit a poll crossing that event. Caller skips tracking after Freeze.
        public bool BeginPoll()
        {
            if (_failed != 0 || _phase == 2 || _pollActive != 0)
                return Reject();
            _pollActive = 1;
            return true;
        }

        public bool EndPoll()
        {
            if (_failed != 0 || _pollActive != 1)
                return Reject();
            _pollActive = 0;
            return true;
        }

        /// <summary>Copy only a successfully frozen partition. The completed
        /// and clipped counts are exclusive; an envelope crossing both window
        /// boundaries counts once as clipped. DispatchNonPoll includes guest
        /// execution AND kernel validation, transitions and synchronous logging;
        /// it is not pure guest CPU. Poll includes both nested and supervisor
        /// calls exactly once. Missing Freeze never supplies a complete result.</summary>

        public readonly bool Inspect(out DiskDispatchTotals totals)
        {
            totals = _phase == 2 ? new(_elapsed, _poll, _dispatch, _nested, _completed, _clipped, _physicalAtFreeze != 0) : default;
            return IsComplete;
        }

        /// <summary>Partition this frozen dispatch into a nested driver and
        /// its remaining shell, excluding their own nested polls exactly once.
        /// Both values must have successfully frozen the same original time
        /// and cumulative-poll window; the caller separately proves actual
        /// nesting, common clock/owner and serialized storage lifetimes. Late
        /// physical returns leave valid frozen totals unchanged. This readonly
        /// arithmetic grants no execution or measurement authority; rejection
        /// returns zero outputs without invalidating either value.</summary>

        public readonly bool InspectChild(in DiskDispatchMeasurements child, out ulong driverNonPoll, out ulong shellNonPoll)
        {
            driverNonPoll = 0;
            shellNonPoll = 0;
            if (!IsComplete || !child.IsComplete || _phaseStart != child._phaseStart || _phasePoll != child._phasePoll || _elapsed != child._elapsed || _poll != child._poll || child._dispatch > _dispatch || child._nested > _nested || child._nested > child._dispatch)
                return false;
            ulong shell = _dispatch - child._dispatch, shellPoll = _nested - child._nested;
            if (shellPoll > shell)
                return false;
            ulong driver = child._dispatch - child._nested, remainder = shell - shellPoll;
            ulong parentNonPoll = _dispatch - _nested;
            // Difference form checks conservation without adding two ulong totals.
            if (driver > parentNonPoll || remainder != parentNonPoll - driver)
                return false;
            driverNonPoll = driver;
            shellNonPoll = remainder;
            return true;
        }

        private bool AddSegment(ulong now, ulong poll, bool clipped)
        {
            if (now < _segmentStart || poll < _segmentPoll)
                return Reject();
            ulong elapsed = now - _segmentStart, nested = poll - _segmentPoll;
            if (nested > elapsed)
                return Reject();
            if (elapsed == 0)
                return true; // Empty half-open boundary contributes no envelope.
            if (_dispatch > ulong.MaxValue - elapsed || _nested > ulong.MaxValue - nested || clipped && _clipped == ulong.MaxValue || !clipped && _completed == ulong.MaxValue)
                return Reject();
            _dispatch += elapsed;
            _nested += nested;
            if (clipped)
                _clipped++;
            else
                _completed++;
            return true;
        }

        private bool Observe(ulong now, ulong poll)
        {
            if (_failed != 0 || now < _lastTime || poll < _lastPoll)
                return Reject();
            _lastTime = now;
            _lastPoll = poll;
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
