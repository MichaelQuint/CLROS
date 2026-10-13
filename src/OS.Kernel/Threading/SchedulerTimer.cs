using OS.Drivers;

namespace OS.Kernel.Threading;

/// <summary>
/// Caller-owned, single-BSP xAPIC one-shot timer control over the existing masked
/// APIC setup. The owner serializes calls with IF clear and retains the UC/RW/NX
/// mapping, native MMIO callbacks, complete interrupt gates/stacks and saved
/// GPR/FS/GS/x87/SSE state until successful Restore. Copying a prepared timer is
/// unsupported. Owner keys catch misuse; they do not establish hardware authority.
/// This type never enables IF, installs an IDT, switches context or publishes a
/// root/wake. Simulated callbacks establish host contracts only, not interrupts.
/// No SMP, x2APIC, wall-clock calibration, deep-idle or upstream GC support.
/// </summary>
public unsafe struct SchedulerTimer
{
    // Intel SDM Vol3A section 11.5.4: one-shot, divide by16 (bits0/1=3),
    // reload through initial count; writing zero stops the counter.
    public const byte Vector = 0xe1;
    public const uint DivideBy16 = 3;
    private const uint Mask = 1U << 16, IsrBit = 2;

    public enum Status : byte
    {
        Ok, Uninitialized, AlreadyPrepared, InvalidOwner, WrongOwner,
        InvalidConfiguration, InvalidAccess, InterruptsEnabled, PathsNotReady,
        NotArmed, AlreadyArmed, DirtyState, ReadFailed, WriteFailed,
        ReadBackFailed, BaselineMismatch, PendingInterrupt, NotTimerInterrupt,
        InvalidInterruptedFlags, InvalidPhase, InvalidDeadlineQueue,
        ClockMismatch, TickOverflow, DeferralLimit
    }

    /// <summary>Observed publication state of the interrupted context. The native
    /// owner sets this before each allocation/publication boundary with IF clear;
    /// Safe permits context redirection only after lifetime/root admission. Other
    /// phases retain the interrupted context and queue, for a bounded tick count.</summary>
    public enum Phase : byte { Safe, Allocating, Initializing, Publishing }
    public enum Action : byte { None, Rearmed, Deferred, WakeReady }

    /// <summary>Acknowledged tick and earliest due identity by value. WakeReady
    /// does not drain the queue or grant restore/root authority: the owner must
    /// revalidate its live context, commit the wake, then cancel/drain the entry.
    /// Failure retains the hardware borrow and may have stopped/acknowledged it.</summary>
    public readonly struct Result
    {
        public readonly Status Code;
        public readonly Action Next;
        public readonly ulong Tick;
        public readonly DeadlineQueue.Entry Due;
        public readonly bool Succeeded => Code == Status.Ok && Next != Action.None;
        public Result(Status code, Action next = Action.None, ulong tick = 0, DeadlineQueue.Entry due = default)
        { Code = code; Next = next; Tick = tick; Due = due; }
    }

    private XApicSetupAccess _access;
    private ulong _owner, _base, _ticks;
    private uint _reload, _maximumDeferredTicks, _deferredTicks, _originalDivide, _originalLvt;
    private bool _prepared, _dirty, _armed;
    public readonly bool IsPrepared => _prepared;
    public readonly bool IsArmed => _armed;
    public readonly bool NeedsRestore => _dirty;
    public readonly ulong Ticks => _ticks;
    public readonly uint DeferredTicks => _deferredTicks;

    /// <summary>
    /// Borrow a previously qualified masked legacy xAPIC with IF clear. The
    /// caller authenticates BSP, CPUID/MSR, MMIO and callbacks independently.
    /// Positive reload is an APIC count, not a duration. initialTick starts the
    /// same nonwrapping epoch as DeadlineQueue. Invalid/read-failed admission
    /// performs no MMIO writes and preserves this value. Baseline must have a
    /// stopped masked timer, TPRd0/SVR1ff, empty ISR and no deliverable classE/F IRR.
    /// Lower blocked IRR is retained, as in the existing qualified APIC setup.
    /// </summary>
    public Status Prepare(ulong owner, ulong localApicAddress, uint reload, uint maximumDeferredTicks,
        XApicSetupAccess access, ulong rflags, ulong initialTick = 0)
    {
        if (_prepared) return Status.AlreadyPrepared;
        if (owner == 0) return Status.InvalidOwner;
        if (localApicAddress < 0x100000 || localApicAddress > 0xfffff000 ||
            (localApicAddress & 4095) != 0 || reload == 0 || maximumDeferredTicks == 0)
            return Status.InvalidConfiguration;
        if (access.Read32 == null || access.Write32 == null) return Status.InvalidAccess;
        if (!IfClear(rflags)) return Status.InterruptsEnabled;
        SchedulerTimer candidate = default;
        candidate._access = access; candidate._base = localApicAddress;
        uint lvt = 0, divide = 0, count = 0, current = 0, tpr = 0, svr = 0;
        if (!candidate.Read(0x320, &lvt) || !candidate.Read(0x3e0, &divide) ||
            !candidate.Read(0x380, &count) || !candidate.Read(0x390, &current) ||
            !candidate.Read(0x80, &tpr) || !candidate.Read(0xf0, &svr)) return Status.ReadFailed;
        if (lvt != BoundedXApicSetup.MaskedVector || (divide & ~0xbU) != 0 || count != 0 || current != 0 ||
            tpr != BoundedXApicSetup.TaskPriority || svr != BoundedXApicSetup.SpuriousValue)
            return Status.BaselineMismatch;
        Status quiet = candidate.CheckPending(false);
        if (quiet != Status.Ok) return quiet;
        candidate._owner = owner; candidate._reload = reload;
        candidate._maximumDeferredTicks = maximumDeferredTicks; candidate._ticks = initialTick;
        candidate._originalDivide = divide; candidate._originalLvt = lvt; candidate._prepared = true;
        this = candidate;
        return Status.Ok;
    }

    /// <summary>
    /// Arm with IF clear only after the live adapter has authenticated complete
    /// timer/spurious/fault gates, owned interrupt stack and full interrupted-state
    /// capture/restore. pathsReady records that external observation; a true value
    /// alone proves none of it. No STI occurs. Admission failures precede writes;
    /// once a write is attempted, every failure retains NeedsRestore, including
    /// callbacks that report failure after a physical write. Restore is required.
    /// </summary>
    public Status Arm(ulong owner, ulong rflags, bool pathsReady)
    {
        Status status = Access(owner, rflags);
        if (status != Status.Ok) return status;
        if (_armed) return Status.AlreadyArmed;
        if (_dirty) return Status.DirtyState;
        if (!pathsReady) return Status.PathsNotReady;
        status = Baseline();
        if (status != Status.Ok) return status;
        status = CheckPending(false);
        if (status != Status.Ok) return status;
        _dirty = true;
        status = WriteChecked(0x320, Mask | Vector);
        if (status == Status.Ok) status = WriteChecked(0x3e0, DivideBy16);
        if (status == Status.Ok) status = WriteChecked(0x320, Vector);
        if (status == Status.Ok) status = WriteChecked(0x380, _reload);
        _armed = status == Status.Ok;
        return status;
    }

    /// <summary>
    /// Run on the owned timer interrupt stack after full architectural capture,
    /// with current IF clear and saved interrupted IF set. Require exactly vector
    /// E1 in ISR, expired one-shot count and unchanged timer/TPR/SVR ownership.
    /// Acknowledge and verify ISR clear before advancing the tick or rearming.
    /// Peek the original queue each time: cancellation stays authoritative and no
    /// due entry is consumed during a critical phase. A safe due tick leaves the
    /// timer stopped for the native owner to redirect the actual IRETQ frame.
    /// Invalid hardware/queue/deferral/wrap results retain ownership for IF-clear
    /// cleanup; they never authorize a wake or another arm. Tick count represents
    /// successfully acknowledged interrupts, not elapsed seconds or host time.
    /// </summary>
    public Result HandleInterrupt(ulong owner, ulong rflags, ulong interruptedFlags,
        ref DeadlineQueue deadlines, ulong deadlineOwner, Phase phase)
    {
        Status status = Access(owner, rflags);
        if (status != Status.Ok) return new(status);
        if (!_armed) return new(Status.NotArmed);
        status = ActiveTimer();
        if (status != Status.Ok) return Fault(status);
        status = CheckPending(true);
        if (status != Status.Ok) return Fault(status);
        // EOI is write-only. Its completion is proved by the subsequent ISR read,
        // never by reading the EOI register or assuming a failed callback did nothing.
        if (_access.Write32(_access.Context, _base + 0xb0, 0) != 1) return Fault(Status.WriteFailed);
        status = CheckPending(false);
        if (status != Status.Ok) return Fault(status);
        if (_ticks == ulong.MaxValue) return Fault(Status.TickOverflow);
        _ticks++;
        if ((interruptedFlags & 0x202) != 0x202) return Fault(Status.InvalidInterruptedFlags);
        if ((byte)phase > (byte)Phase.Publishing) return Fault(Status.InvalidPhase);
        DeadlineQueue.Result earliest = deadlines.Peek(deadlineOwner);
        if (earliest.Code != DeadlineQueue.Status.Ok && earliest.Code != DeadlineQueue.Status.Empty)
            return Fault(Status.InvalidDeadlineQueue);
        if (deadlines.ObservedTicks > _ticks) return Fault(Status.ClockMismatch);
        bool due = earliest.Code == DeadlineQueue.Status.Ok && earliest.Value.Deadline <= _ticks;
        if (phase != Phase.Safe)
        {
            if (_deferredTicks == _maximumDeferredTicks) return Fault(Status.DeferralLimit);
            _deferredTicks++;
        }
        else
        {
            _deferredTicks = 0;
            if (due)
            {
                _armed = false;
                return new(Status.Ok, Action.WakeReady, _ticks, earliest.Value);
            }
        }
        status = WriteChecked(0x380, _reload);
        if (status != Status.Ok) return Fault(status);
        return new(Status.Ok, phase == Phase.Safe ? Action.Rearmed : Action.Deferred, _ticks);
    }

    /// <summary>
    /// With IF clear, mask then stop the timer and require empty ISR/no deliverable
    /// IRR before restoring its original divider/masked LVT. Verify all timer and
    /// retained TPR/SVR registers before releasing this borrow. A failure retains
    /// IsPrepared/NeedsRestore and all external IDT/stack/mapping lifetimes; retry
    /// only after the owner resolves the cause. Pending IRQs are never silently
    /// acknowledged here. Success clears this value, without freeing any storage.
    /// The native owner separately restores IDT/TSS/context and original IF.
    /// </summary>
    public Status Restore(ulong owner, ulong rflags)
    {
        Status status = Access(owner, rflags);
        if (status != Status.Ok) return status;
        if (_dirty)
        {
            _armed = false;
            status = WriteChecked(0x320, Mask | Vector);
            if (status == Status.Ok) status = WriteChecked(0x380, 0);
            if (status != Status.Ok) return status;
            uint current = 0;
            if (!Read(0x390, &current)) return Status.ReadFailed;
            if (current != 0) return Status.ReadBackFailed;
        }
        status = CheckPending(false);
        if (status != Status.Ok) return status;
        if (_dirty)
        {
            status = WriteChecked(0x3e0, _originalDivide);
            if (status == Status.Ok) status = WriteChecked(0x320, _originalLvt);
            if (status != Status.Ok) return status;
        }
        status = Baseline();
        if (status != Status.Ok) return status;
        status = CheckPending(false);
        if (status != Status.Ok) return status;
        this = default;
        return Status.Ok;
    }

    private readonly Status Access(ulong owner, ulong flags) => !_prepared ? Status.Uninitialized :
        owner != _owner ? Status.WrongOwner : !IfClear(flags) ? Status.InterruptsEnabled : Status.Ok;
    private static bool IfClear(ulong flags) => (flags & 0x202) == 2;
    private Result Fault(Status code) { _armed = false; return new(code, tick: _ticks); }
    private readonly bool Read(uint offset, uint* value) => _access.Read32(_access.Context, _base + offset, value) == 1;
    private readonly Status WriteChecked(uint offset, uint value)
    {
        if (_access.Write32(_access.Context, _base + offset, value) != 1) return Status.WriteFailed;
        uint actual = 0;
        if (!Read(offset, &actual)) return Status.ReadFailed;
        return actual == value ? Status.Ok : Status.ReadBackFailed;
    }
    private readonly Status Baseline() => Registers(_originalLvt, _originalDivide, 0);
    private readonly Status ActiveTimer() => Registers(Vector, DivideBy16, _reload);
    private readonly Status Registers(uint expectedLvt, uint expectedDivide, uint expectedInitial)
    {
        uint lvt = 0, divide = 0, initial = 0, current = 0, tpr = 0, svr = 0;
        if (!Read(0x320, &lvt) || !Read(0x3e0, &divide) || !Read(0x380, &initial) ||
            !Read(0x390, &current) || !Read(0x80, &tpr) || !Read(0xf0, &svr)) return Status.ReadFailed;
        return lvt == expectedLvt && divide == expectedDivide && initial == expectedInitial && current == 0 &&
            tpr == BoundedXApicSetup.TaskPriority && svr == BoundedXApicSetup.SpuriousValue
            ? Status.Ok : Status.BaselineMismatch;
    }
    private readonly Status CheckPending(bool timerInService)
    {
        uint value = 0;
        for (uint i = 0; i < 8; i++)
        {
            if (!Read(0x100 + i * 16, &value)) return Status.ReadFailed;
            uint expected = timerInService && i == 7 ? IsrBit : 0;
            if (value != expected) return timerInService ? Status.NotTimerInterrupt : Status.PendingInterrupt;
        }
        if (!Read(0x270, &value)) return Status.ReadFailed;
        return value == 0 ? Status.Ok : Status.PendingInterrupt;
    }
}
