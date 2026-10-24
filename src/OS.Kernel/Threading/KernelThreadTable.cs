using OS.Pal;

namespace OS.Kernel.Threading;

/// <summary>
/// Single-BSP CPL0 cooperative lifecycle over caller-owned slots, contexts,
/// guarded stacks and ready/deadline buffers. One stable, noncopied table owns
/// every operation. Its trusted caller saves/disables/restores IF across the
/// complete transaction and native transfer; supplied flags are observations,
/// not instructions. Keys detect accidental misuse, not forgery or concurrency.
/// No allocation, hardware access, callback, transfer or GC scanning occurs here.
/// Entry and captured CALL contexts use ThreadContextSwitch admission; IRQ frames,
/// application threads, arbitrary asynchronous resumption and SMP are unsupported.
/// Keep all borrowed ranges mapped/exclusive until release, and never reuse an
/// owner key for another table lifetime. Native execution remains a separate proof.
/// </summary>
public unsafe struct KernelThreadTable
{
    public enum Status : byte
    {
        Ok, Pending, Uninitialized, AlreadyInitialized, WrongOwner, CopiedOwner,
        InterruptsEnabled, InvalidStorage, InvalidCapacity, InvalidContext,
        InvalidEntry, Overlap, Full, ReadyFull, DeadlineFull, InvalidIdentity,
        StaleIdentity, WrongState, NotCurrent, Busy, Empty, ClockRegression,
        DeadlineOverflow, IdentityExhausted, StaleWait, AlreadyCompleted,
        SelfJoin, JoinCycle, JoinBusy, Cancelled, Pinned, NotSupervisor,
        QueueFailure, NotEmpty, TimerFailure
    }
    public enum State : byte { Empty, Ready, Running, Parking, Waiting, Pending, Exiting, Exited, Retired }
    public enum WaitKind : byte { None, Park, Sleep, Join }
    public enum Completion : byte { None, Woken, Deadline, Joined, Cancelled, Interrupted }

    /// <summary>By-value operation/snapshot result. Default is not successful.
    /// Pending means one completion won and is retained until ready space exists.
    /// Context/entry are borrowed descriptions, never execution or release grants.
    /// ExitValue belongs to this thread; JoinedExitValue is the copied join target
    /// value, so a later exit cannot be confused with an earlier joined outcome.
    /// Only successful Reap returns storage whose contents have been scrubbed.</summary>
    public readonly struct Result
    {
        public readonly Status Code;
        public readonly OSHostHandle Identity;
        public readonly KernelThreadWait Wait;
        public readonly State State;
        public readonly Completion Completion;
        public readonly bool CancellationRequested;
        public readonly int ExitValue, JoinedExitValue;
        public readonly uint Pins;
        public readonly ulong Entry, Argument;
        public readonly ThreadContextSwitch.Descriptor Context;
        private readonly bool _returned;
        public bool Succeeded => _returned && (Code == Status.Ok || Code == Status.Pending);
        internal Result(Status code, OSHostHandle identity = default, KernelThreadSlot slot = default)
        {
            Code = code; Identity = identity; State = slot.State; Completion = slot.Completion;
            Wait = slot.Sequence == 0 ? default : new(identity, slot.Sequence);
            CancellationRequested = slot.Cancellation; ExitValue = slot.ExitValue;
            JoinedExitValue = slot.JoinedExitValue;
            Pins = slot.Pins; Entry = slot.Entry; Argument = slot.Argument; Context = slot.Context; _returned = true;
        }
    }

    public const uint MaximumSlots = 64;
    private KernelThreadSlot* _slots;
    private OSHostHandle* _readyStorage;
    private DeadlineQueue.Entry* _deadlineStorage;
    private uint _capacity, _slotBytes, _readyBytes, _deadlineBytes;
    private ulong _key, _self, _now;
    private bool _initialized;
    private OSHostHandle _current, _handoff;
    private ThreadContextSwitch.Owner _switchOwner;
    private ThreadContextSwitch.Descriptor _supervisor;
    private ReadyQueue _ready;
    private DeadlineQueue _deadlines;

    public readonly uint ReadyCount => _ready.Count;
    public readonly uint DeadlineCount => _deadlines.Count;
    public readonly ulong ObservedTicks => _now;
    public readonly OSHostHandle Current => _current;
    public readonly bool HandoffPending => _handoff.IsValid;

#if !NOSTDLIB_EFI || M16_027_PROBE
    /// <summary>Bounded copied retained-storage inventory for an authenticated
    /// IRQ-off lifetime owner. Includes metadata, queue buffers, context arena,
    /// switch code/stack, supervisor context/stack and every retained worker
    /// context/stack. Empty slots report zero extents. These scalar observations
    /// grant no mapping, copy, transfer, release or new borrowing authority.
    /// Keep the same serialized transaction through the outer alias checks.</summary>
    public Status InspectRetainedExtent(ulong key, ulong flags, uint index, out ulong start, out ulong bytes)
    {
        start = bytes = 0;
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        if (index >= 9 + 2 * _capacity) return Status.InvalidStorage;
        switch (index)
        {
            case 0: start = _self; bytes = (uint)sizeof(KernelThreadTable); break;
            case 1: start = (ulong)_slots; bytes = _slotBytes; break;
            case 2: start = (ulong)_readyStorage; bytes = _readyBytes; break;
            case 3: start = (ulong)_deadlineStorage; bytes = _deadlineBytes; break;
            case 4: start = _switchOwner.ContextArena; bytes = _switchOwner.ContextArenaBytes; break;
            case 5: start = _switchOwner.CodeStart; bytes = _switchOwner.CodeBytes; break;
            case 6: start = _switchOwner.SwitchStackGuardStart; bytes = _switchOwner.SwitchStackBytes; break;
            case 7: start = (ulong)_supervisor.Context; bytes = _supervisor.ContextBytes; break;
            case 8: start = _supervisor.StackGuardStart; bytes = _supervisor.StackBytes; break;
            default:
                var slot = _slots[(index - 9) / 2];
                if (slot.State is State.Empty or State.Retired) break;
                if ((index & 1) != 0) { start = (ulong)slot.Context.Context; bytes = slot.Context.ContextBytes; }
                else { start = slot.Context.StackGuardStart; bytes = slot.Context.StackBytes; }
                break;
        }
        return Status.Ok;
    }

    /// <summary>Read the current bounded inventory length under original key,
    /// address and IRQ-off ownership. At most137 extents; zero denotes invalid
    /// ownership. The count supplies no lifetime pin or access authority.</summary>
    public uint RetainedExtentCount(ulong key, ulong flags) => Access(key, flags) == Status.Ok ? 9 + 2 * _capacity : 0;
#endif

    /// <summary>
    /// Borrow disjoint mapped metadata/buffers and a retained admitted supervisor.
    /// Its slot identity must be outside worker slots 1..capacity. Initial seeds
    /// belong to the trusted lifetime owner, allowing exhaustion tests/recovery
    /// without wrap; handles are scoped to this unique owner lifetime. All checks
    /// precede buffer writes. Success initializes slots, not context/stack pages.
    /// Queue capacities may be smaller than slot capacity; full wakes stay pending.
    /// </summary>
    public Status Initialize(ulong key, ulong flags, ThreadContextSwitch.Owner switchOwner,
        ThreadContextSwitch.Descriptor supervisor, KernelThreadSlot* slots, uint slotBytes, uint capacity,
        OSHostHandle* ready, uint readyBytes, uint readyCapacity, DeadlineQueue.Entry* deadlines,
        uint deadlineBytes, uint deadlineCapacity, ulong now = 0, uint generation = 1, ulong sequence = 0)
    {
        if (_initialized) return Status.AlreadyInitialized;
        if (key == 0 || key != switchOwner.Key) return Status.WrongOwner;
        if (!IfClear(flags)) return Status.InterruptsEnabled;
        if (capacity == 0 || capacity > MaximumSlots || readyCapacity == 0 || readyCapacity > capacity ||
            deadlineCapacity == 0 || deadlineCapacity > capacity || generation == 0) return Status.InvalidCapacity;
        if (ThreadContextSwitch.AdmitResume(switchOwner, supervisor) != ThreadContextSwitch.Status.Ok ||
            (uint)supervisor.Identity <= capacity) return Status.InvalidContext;
        if (!Buffer((ulong)slots, slotBytes, (ulong)capacity * (uint)sizeof(KernelThreadSlot)) ||
            !Buffer((ulong)ready, readyBytes, readyCapacity * 8UL) ||
            !Buffer((ulong)deadlines, deadlineBytes, (ulong)deadlineCapacity * (uint)sizeof(DeadlineQueue.Entry)))
            return Status.InvalidStorage;
        fixed (KernelThreadTable* self = &this)
        {
            ulong metadata = (ulong)self;
            if (!Buffer(metadata, (uint)sizeof(KernelThreadTable), (uint)sizeof(KernelThreadTable))) return Status.InvalidStorage;
            ulong* starts = stackalloc ulong[4] { metadata, (ulong)slots, (ulong)ready, (ulong)deadlines };
            ulong* lengths = stackalloc ulong[4] { (uint)sizeof(KernelThreadTable), slotBytes, readyBytes, deadlineBytes };
            for (uint i = 0; i < 4; i++)
            {
                if (Overlap(starts[i], lengths[i], switchOwner.CodeStart, switchOwner.CodeBytes) ||
                    Overlap(starts[i], lengths[i], switchOwner.SwitchStackGuardStart, switchOwner.SwitchStackBytes) ||
                    Overlap(starts[i], lengths[i], supervisor.StackGuardStart, supervisor.StackBytes) ||
                    Overlap(starts[i], lengths[i], (ulong)supervisor.Context, supervisor.ContextBytes)) return Status.Overlap;
                for (uint j = 0; j < i; j++)
                    if (Overlap(starts[i], lengths[i], starts[j], lengths[j])) return Status.Overlap;
            }
            ReadyQueue readyQueue = default; DeadlineQueue deadlineQueue = default;
            if (readyQueue.Initialize(key, ready, readyBytes, readyCapacity) != ReadyQueue.Status.Ok ||
                deadlineQueue.Initialize(key, deadlines, deadlineBytes, deadlineCapacity, now) != DeadlineQueue.Status.Ok)
                return Status.InvalidStorage;
            _slots = slots; _capacity = capacity; _slotBytes = slotBytes;
            _readyStorage = ready; _readyBytes = readyBytes; _deadlineStorage = deadlines; _deadlineBytes = deadlineBytes;
            _ready = readyQueue; _deadlines = deadlineQueue; _switchOwner = switchOwner; _supervisor = supervisor;
            _key = key; _self = metadata; _now = now; _current = default; _handoff = default;
            for (uint i = 0; i < capacity; i++) _slots[i] = new() { Generation = generation, Sequence = sequence };
            _initialized = true;
        }
        return Status.Ok;
    }

    /// <summary>Read the first empty slot's prospective generation/slot identity
    /// without changing storage. Failure returns a default identity, including
    /// full slot or ready capacity. This acquires no reservation, pin or publication
    /// authority and does not admit a context. Keep the same single-owner IF-clear
    /// transaction through Create; another publication can change this result.</summary>
    public Status InspectNextCreateIdentity(ulong key, ulong flags, out OSHostHandle identity)
    {
        identity = default;
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        uint index = 0; while (index < _capacity && _slots[index].State != State.Empty) index++;
        if (index == _capacity) return Status.Full;
        if (_ready.IsFull) return Status.ReadyFull;
        identity = Id(index); return Status.Ok;
    }

    /// <summary>
    /// Publish a complete preinitialized entry binding into the first vacant slot.
    /// This slice admits mechanical worker entry code inside the owned code range.
    /// Failed stack/context/entry/alias/capacity admission borrows nothing and leaves
    /// slots, queues and caller resources unchanged. Caller allocation failures
    /// therefore cannot leak a published context or runnable partial binding.
    /// </summary>
    public Result Create(ulong key, ulong flags, X64ThreadContext* context, uint contextBytes,
        ulong stackGuard, ulong stackBytes, ulong trampoline, ulong entry, ulong argument)
    {
        Status access = Access(key, flags); if (access != Status.Ok) return new(access);
        uint index = 0; while (index < _capacity && _slots[index].State != State.Empty) index++;
        if (index == _capacity) return new(Status.Full);
        if (!Contains(_switchOwner.CodeStart, _switchOwner.CodeBytes, entry, 1)) return new(Status.InvalidEntry);
        OSHostHandle identity = Id(index);
        ThreadContextSwitch.Descriptor value = new() { Identity = identity.Value, CurrentIdentity = identity.Value,
            Context = context, ContextBytes = contextBytes, StackGuardStart = stackGuard, StackBytes = stackBytes,
            ResumeRip = trampoline, Kind = ThreadContextSwitch.ResumeKind.Entry };
        if (ThreadContextSwitch.AdmitResume(_switchOwner, value) != ThreadContextSwitch.Status.Ok) return new(Status.InvalidContext);
        if (ResourceOverlap(value)) return new(Status.Overlap);
        if (_ready.IsFull) return new(Status.ReadyFull);
        KernelThreadSlot candidate = _slots[index]; candidate.Context = value; candidate.Entry = entry;
        candidate.Argument = argument; candidate.State = State.Ready;
        KernelThreadSlot previous = _slots[index];
        _slots[index] = candidate; // Initialize binding before runnable publication, including under future instrumentation.
        if (Enqueue(identity, candidate) != Status.Ok)
        { _slots[index] = previous; return new(Status.QueueFailure); }
        return new(Status.Ok, identity, candidate);
    }

    /// <summary>Read one current generation without changing lifetime. Empty,
    /// retired or reused identities reject. This does not pin the returned data.</summary>
    public Result Inspect(ulong key, ulong flags, OSHostHandle identity)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return new(status);
        status = Lookup(identity, out uint index);
        return status == Status.Ok ? new(Status.Ok, identity, _slots[index]) : new(status);
    }

    /// <summary>Choose one ready context only from the retained supervisor state.
    /// An unresolved native handoff blocks dispatch. The owner evicts stale ready
    /// heads explicitly; invalid live context remains queued for fail-closed repair.
    /// Cancellation is reported for the entry/safe-point adapter to route to exit.</summary>
    public Result Dispatch(ulong key, ulong flags)
    {
        Status access = Access(key, flags); if (access != Status.Ok) return new(access);
        if (_current.IsValid || _handoff.IsValid) return new(Status.Busy);
        for (uint n = 0; n < _capacity; n++)
        {
            ReadyQueue.Result first = _ready.Peek(key);
            if (first.Code == ReadyQueue.Status.Empty) return new(Status.Empty);
            if (!first.Succeeded) return new(Status.QueueFailure);
            if (Lookup(first.Identity, out uint index) != Status.Ok || _slots[index].State != State.Ready)
            { if (_ready.Cancel(key, first.Identity) != ReadyQueue.Status.Ok) return new(Status.QueueFailure); continue; }
            if (ThreadContextSwitch.AdmitResume(_switchOwner, _slots[index].Context) != ThreadContextSwitch.Status.Ok)
                return new(Status.InvalidContext);
            if (!_ready.Dequeue(key, first.Identity).Succeeded) return new(Status.QueueFailure);
            _slots[index].State = State.Running; _current = first.Identity;
            return new(Status.Ok, first.Identity, _slots[index]);
        }
        return new(Status.Empty);
    }

    /// <summary>Yield one freshly captured CALL context. Reserve ready space before
    /// changing state; failure leaves it current. Success requires native return
    /// and ConfirmSupervisor before another dispatch or reclamation.</summary>
    public Result Yield(ulong key, ulong flags, OSHostHandle identity, ThreadContextSwitch.Descriptor captured)
    {
        Status status = Running(key, flags, identity, out uint index); if (status != Status.Ok) return new(status);
        status = Capture(index, captured); if (status != Status.Ok) return new(status);
        if (_ready.IsFull) return new(Status.ReadyFull);
        KernelThreadSlot next = _slots[index]; next.Context = captured; next.State = State.Ready;
        if (Enqueue(identity, next) != Status.Ok) return new(Status.QueueFailure);
        _slots[index] = next; Handoff(identity); return new(Status.Ok, identity, next);
    }

    /// <summary>
    /// Register then park within one external IRQ-off transaction. Sleep ticks use
    /// checked nonwrapping deadlines; zero sleep is handled by Sleep as yield.
    /// At most one joiner is pending per target; completed joins return the retained
    /// exit value without parking. Self/cyclic/second joins fail before mutation.
    /// Caller must finish Park before restoring IF or executing unrelated code.
    /// </summary>
    public Result BeginWait(ulong key, ulong flags, OSHostHandle identity, WaitKind kind,
        ulong now, ulong delay = 0, OSHostHandle target = default, bool timedJoin = false)
    {
        Status status = Running(key, flags, identity, out uint index); if (status != Status.Ok) return new(status);
        if (_slots[index].Cancellation) return new(Status.Cancelled);
        if (kind != WaitKind.Park && kind != WaitKind.Sleep && kind != WaitKind.Join) return new(Status.WrongState);
        if (now < _now) return new(Status.ClockRegression);
        bool timed = kind == WaitKind.Sleep || kind == WaitKind.Join && timedJoin;
        if (kind == WaitKind.Sleep && delay == 0) return new(Status.WrongState);
        if (timed && delay > ulong.MaxValue - now) return new(Status.DeadlineOverflow);
        uint targetIndex = 0;
        if (kind == WaitKind.Join)
        {
            if (identity.Value == target.Value) return new(Status.SelfJoin);
            status = Lookup(target, out targetIndex); if (status != Status.Ok) return new(status);
            if (_slots[targetIndex].State == State.Exited)
            {
                KernelThreadSlot observed = _slots[index]; observed.Completion = Completion.Joined;
                observed.JoinedExitValue = _slots[targetIndex].ExitValue;
                return new(Status.Ok, identity, observed);
            }
            if (_slots[targetIndex].Joiner.IsValid) return new(Status.JoinBusy);
            OSHostHandle cursor = target;
            for (uint n = 0; cursor.IsValid && n <= _capacity; n++)
            {
                if (cursor.Value == identity.Value) return new(Status.JoinCycle);
                if (n == _capacity || Lookup(cursor, out uint link) != Status.Ok) return new(Status.JoinCycle);
                cursor = _slots[link].JoinTarget;
            }
        }
        if (_slots[index].Sequence == ulong.MaxValue) return new(Status.IdentityExhausted);
        if (timed && _deadlines.IsFull) return new(Status.DeadlineFull);
        if (timed && _deadlines.Schedule(key, identity, identity, now, delay) != DeadlineQueue.Status.Ok)
            return new(Status.QueueFailure);
        _slots[index].Sequence++; _slots[index].WaitKind = kind; _slots[index].Completion = Completion.None;
        _slots[index].JoinedExitValue = 0; _slots[index].HasDeadline = timed;
        _slots[index].JoinTarget = kind == WaitKind.Join ? target : default;
        if (kind == WaitKind.Join) _slots[targetIndex].Joiner = identity;
        _slots[index].State = State.Parking; _now = now;
        return new(Status.Ok, identity, _slots[index]);
    }

    /// <summary>Commit a registered wait using its freshly captured CALL context.
    /// An earlier wake/cancellation winner resumes in place without a handoff.
    /// A parked worker remains pinned by the handoff until supervisor confirmation.</summary>
    public Result Park(ulong key, ulong flags, KernelThreadWait wait, ThreadContextSwitch.Descriptor captured)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return new(status);
        status = WaitSlot(wait, out uint index); if (status != Status.Ok) return new(status);
        if (_slots[index].State != State.Parking || _current.Value != wait.Thread.Value) return new(Status.WrongState);
        status = Capture(index, captured); if (status != Status.Ok) return new(status);
        _slots[index].Context = captured;
        if (_slots[index].Completion != Completion.None) _slots[index].State = State.Running;
        else { _slots[index].State = State.Waiting; Handoff(wait.Thread); }
        return new(Status.Ok, wait.Thread, _slots[index]);
    }

    /// <summary>Zero delay yields without a deadline; positive delay registers and
    /// parks in one transaction. Invalid capture is rejected before registration.</summary>
    public Result Sleep(ulong key, ulong flags, OSHostHandle identity,
        ThreadContextSwitch.Descriptor captured, ulong now, ulong delay)
    {
        if (delay == 0) return Yield(key, flags, identity, captured);
        Status status = Running(key, flags, identity, out uint index); if (status != Status.Ok) return new(status);
        status = Capture(index, captured); if (status != Status.Ok) return new(status);
        Result begin = BeginWait(key, flags, identity, WaitKind.Sleep, now, delay);
        return begin.Succeeded ? Park(key, flags, begin.Wait, captured) : begin;
    }

    /// <summary>Complete a park/sleep registration, including wake-before-park.
    /// Join completion belongs to target exit. Full ready capacity retains exactly
    /// one winner in Pending; Flush publishes it later without losing the wake.</summary>
    public Result Wake(ulong key, ulong flags, KernelThreadWait wait)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return new(status);
        status = WaitSlot(wait, out uint index); if (status != Status.Ok) return new(status);
        if (_slots[index].WaitKind == WaitKind.Join) return new(Status.WrongState);
        return Complete(wait, Completion.Woken);
    }

    /// <summary>Complete a park/sleep registration for a genuine externally
    /// selected signal, without setting thread cancellation. The IRQ-off owner
    /// supplies the exact generation/sequence and resolves pending-signal policy.
    /// An earlier wake/deadline/interrupt remains the sole winner; ready overflow
    /// retains Pending. No handler, user frame, native transfer or join interrupt
    /// is performed. All context/stack borrows retain their existing lifetime.</summary>
    public Result Interrupt(ulong key, ulong flags, KernelThreadWait wait)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return new(status);
        status = WaitSlot(wait, out uint index); if (status != Status.Ok) return new(status);
        if (_slots[index].WaitKind == WaitKind.Join) return new(Status.WrongState);
        return Complete(wait, Completion.Interrupted);
    }

    /// <summary>Advance one injected/hardware-owner tick epoch and complete due
    /// waits in deadline order. No hardware is accessed. A valid tick is retained
    /// even if a later context rejects; earlier completed waits are not rolled back.
    /// Pending publication keeps its winner after unlink, without lost wakeups.</summary>
    public Status Advance(ulong key, ulong flags, ulong now)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        if (now < _now) return Status.ClockRegression;
        _now = now;
        while (true)
        {
            DeadlineQueue.Result first = _deadlines.Peek(key);
            if (first.Code == DeadlineQueue.Status.Empty) break;
            if (!first.Succeeded) return Status.QueueFailure;
            if (first.Value.Deadline > now) break;
            if (Lookup(first.Value.Identity, out uint index) != Status.Ok || !_slots[index].HasDeadline)
            { if (_deadlines.Cancel(key, first.Value.Identity) != DeadlineQueue.Status.Ok) return Status.QueueFailure; continue; }
            Result done = Complete(new(first.Value.Identity, _slots[index].Sequence), Completion.Deadline);
            if (!done.Succeeded) return done.Code;
        }
        return Status.Ok;
    }

    /// <summary>Owned idle interrupt transaction over the private deadline queue.
    /// The native caller has captured the separate IRQ frame, authenticated the
    /// APIC/gates/stack, and disabled IF. No worker or unresolved handoff may be
    /// active. Timer and table must share the acknowledged tick epoch. A successful
    /// safe tick commits due completions; full ready capacity retains Pending.
    /// TimerFailure preserves the detailed timer result and its hardware borrow.
    /// Any failure after MMIO requires native quiescence/Restore before release.
    /// This performs no idle instruction, frame redirect or context transfer.</summary>
    public Status HandleIdleTimer(ulong key, ulong flags, ref SchedulerTimer timer, ulong timerOwner,
        ulong interruptedFlags, out SchedulerTimer.Result tick)
    {
        tick = default;
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        if (_current.IsValid || _handoff.IsValid) return Status.Busy;
        if (timer.Ticks != _now) return Status.ClockRegression;
        tick = timer.HandleInterrupt(timerOwner, flags, interruptedFlags, ref _deadlines, _key, SchedulerTimer.Phase.Safe);
        return tick.Succeeded ? Advance(key, flags, tick.Tick) : Status.TimerFailure;
    }

    /// <summary>Retry retained completions in slot order. A full queue stops cleanly;
    /// no completed registration is restored and its winner/status remain pinned.</summary>
    public Status Flush(ulong key, ulong flags)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        for (uint i = 0; i < _capacity; i++)
        {
            if (_slots[i].State != State.Pending) continue;
            if (_ready.IsFull) return Status.ReadyFull;
            status = Enqueue(Id(i), _slots[i]); if (status != Status.Ok) return status;
            _slots[i].State = State.Ready;
        }
        return Status.Ok;
    }

    /// <summary>Deferred cancellation, observed by entry/cooperative safe points.
    /// A waiting worker resumes with Cancelled unless a previous event already won.
    /// Cancelling a join releases its registration, never cancels its target.</summary>
    public Result Cancel(ulong key, ulong flags, OSHostHandle identity)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return new(status);
        status = Lookup(identity, out uint index); if (status != Status.Ok) return new(status);
        if (_slots[index].State == State.Exited || _slots[index].State == State.Exiting) return new(Status.WrongState);
        Result completion = new(Status.Ok);
        if ((_slots[index].State == State.Parking || _slots[index].State == State.Waiting) && _slots[index].Completion == Completion.None)
        {
            completion = Complete(new(identity, _slots[index].Sequence), Completion.Cancelled);
            if (!completion.Succeeded) return completion;
        }
        _slots[index].Cancellation = true;
        return new(completion.Code, identity, _slots[index]);
    }

    /// <summary>Record normal return/explicit exit after CALL capture. This does not
    /// release storage or complete a join. Native transfer must reach the retained
    /// supervisor and confirm its stack before exit becomes observable/reapable.</summary>
    public Result Exit(ulong key, ulong flags, OSHostHandle identity, ThreadContextSwitch.Descriptor captured, int value)
    {
        Status status = Running(key, flags, identity, out uint index); if (status != Status.Ok) return new(status);
        status = Capture(index, captured); if (status != Status.Ok) return new(status);
        _slots[index].Context = captured; _slots[index].ExitValue = value; _slots[index].State = State.Exiting;
        Handoff(identity); return new(Status.Ok, identity, _slots[index]);
    }

    /// <summary>Trusted native owner supplies its actual post-transfer RSP, on the
    /// retained supervisor stack with IF clear. Failed confirmation retains handoff
    /// resources. Host acceptance of this observation is not hardware proof.</summary>
    public Status ConfirmSupervisor(ulong key, ulong flags, ulong observedRsp)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        if (_current.IsValid || !_handoff.IsValid || !SupervisorStack(observedRsp)) return Status.NotSupervisor;
        status = Lookup(_handoff, out uint index); if (status != Status.Ok) return status;
        if (_slots[index].State == State.Exiting)
        {
            OSHostHandle joiner = _slots[index].Joiner;
            if (joiner.IsValid)
            {
                status = Lookup(joiner, out uint waiting); if (status != Status.Ok) return status;
                Result completed = Complete(new(joiner, _slots[waiting].Sequence), Completion.Joined, _slots[index].ExitValue);
                if (!completed.Succeeded) return completed.Code;
            }
            _slots[index].State = State.Exited;
        }
        _handoff = default; return Status.Ok;
    }

    /// <summary>Balance an external IRQ/context/root snapshot pin. Pins acquire no
    /// GC behavior; the single owner keeps each pin until its external consumer is
    /// finished. Underflow/overflow/stale identities reject without changing count.</summary>
    public Status Pin(ulong key, ulong flags, OSHostHandle identity, bool acquire)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        status = Lookup(identity, out uint index); if (status != Status.Ok) return status;
        if (acquire ? _slots[index].Pins == uint.MaxValue : _slots[index].Pins == 0) return Status.Pinned;
        if (acquire) _slots[index].Pins++; else _slots[index].Pins--;
        return Status.Ok;
    }

    /// <summary>Supervisor-only scrub of an exited, unreferenced binding. Caller
    /// keeps pages mapped through the complete operation and frees them only after
    /// success. Guard pages are neither read nor written. Context and usable stack
    /// bytes are zeroed; generation advances or the slot retires permanently.</summary>
    public Result Reap(ulong key, ulong flags, OSHostHandle identity, ulong observedRsp)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return new(status);
        if (_current.IsValid || _handoff.IsValid || !SupervisorStack(observedRsp)) return new(Status.NotSupervisor);
        status = Lookup(identity, out uint index); if (status != Status.Ok) return new(status);
        KernelThreadSlot slot = _slots[index];
        if (slot.State != State.Exited) return new(Status.WrongState);
        if (slot.Pins != 0 || slot.Joiner.IsValid || slot.JoinTarget.IsValid || slot.HasDeadline) return new(Status.Pinned);
        for (uint i = 0; i < _capacity; i++)
            if (_slots[i].JoinTarget.Value == identity.Value) return new(Status.Pinned);
        ReadyQueue.Status ready = _ready.Cancel(key, identity);
        if (ready != ReadyQueue.Status.Ok && ready != ReadyQueue.Status.NotFound) return new(Status.QueueFailure);
        DeadlineQueue.Status deadline = _deadlines.Cancel(key, identity);
        if (deadline != DeadlineQueue.Status.Ok && deadline != DeadlineQueue.Status.NotFound) return new(Status.QueueFailure);
        for (uint i = 0; i < slot.Context.ContextBytes; i++) ((byte*)slot.Context.Context)[i] = 0;
        ulong usable = slot.Context.StackBytes - 2UL * ThreadContextSwitch.Page;
        byte* stack = (byte*)(slot.Context.StackGuardStart + ThreadContextSwitch.Page);
        for (ulong i = 0; i < usable; i++) stack[i] = 0;
        _slots[index] = slot.Generation == uint.MaxValue ? new() { Generation = slot.Generation, State = State.Retired }
            : new() { Generation = slot.Generation + 1 };
        return new(Status.Ok, identity, slot);
    }

    /// <summary>End an empty borrow on the supervisor. Released slots/queue entries
    /// contain no live references; resource pages were previously returned by Reap.
    /// A subsequent lifetime requires a new unique owner key and identity seeds.</summary>
    public Status Release(ulong key, ulong flags, ulong observedRsp)
    {
        Status status = Access(key, flags); if (status != Status.Ok) return status;
        if (_current.IsValid || _handoff.IsValid || !SupervisorStack(observedRsp)) return Status.NotSupervisor;
        for (uint i = 0; i < _capacity; i++)
            if (_slots[i].State != State.Empty && _slots[i].State != State.Retired) return Status.NotEmpty;
        if (!_ready.IsEmpty || !_deadlines.IsEmpty) return Status.NotEmpty;
        _ready.Release(key); _deadlines.Release(key); this = default; return Status.Ok;
    }

    private Result Complete(KernelThreadWait wait, Completion reason, int exitValue = 0)
    {
        Status status = WaitSlot(wait, out uint index); if (status != Status.Ok) return new(status);
        KernelThreadSlot slot = _slots[index];
        if (slot.Completion != Completion.None || slot.State == State.Pending || slot.State == State.Ready)
            return new(Status.AlreadyCompleted);
        if (slot.State != State.Parking && slot.State != State.Waiting) return new(Status.StaleWait);
        bool queued = slot.State == State.Waiting && !_ready.IsFull;
        if (slot.State == State.Waiting && ThreadContextSwitch.AdmitResume(_switchOwner, slot.Context) != ThreadContextSwitch.Status.Ok)
            return new(Status.InvalidContext);
        if (queued && Enqueue(wait.Thread, slot) != Status.Ok) return new(Status.QueueFailure);
        if (slot.HasDeadline && _deadlines.Cancel(_key, wait.Thread) != DeadlineQueue.Status.Ok)
        { if (queued) _ready.Cancel(_key, wait.Thread); return new(Status.QueueFailure); }
        if (slot.JoinTarget.IsValid)
        {
            // Target cannot be reaped while its joiner is registered.
            if (Lookup(slot.JoinTarget, out uint target) != Status.Ok)
            { if (queued) _ready.Cancel(_key, wait.Thread); return new(Status.QueueFailure); }
            _slots[target].Joiner = default;
        }
        slot.HasDeadline = false; slot.JoinTarget = default; slot.Completion = reason; slot.JoinedExitValue = exitValue;
        if (slot.State == State.Waiting) slot.State = queued ? State.Ready : State.Pending;
        _slots[index] = slot;
        return new(slot.State == State.Pending ? Status.Pending : Status.Ok, wait.Thread, slot);
    }
    private Status Enqueue(OSHostHandle identity, KernelThreadSlot slot) =>
        _ready.Enqueue(_key, identity, identity, true, slot.Context.Context, slot.Context.ContextBytes,
            _switchOwner.ObservedMxcsrMask) == ReadyQueue.Status.Ok ? Status.Ok : Status.QueueFailure;
    private void Handoff(OSHostHandle identity) { _current = default; _handoff = identity; }
    private Status Capture(uint index, ThreadContextSwitch.Descriptor captured)
    {
        ThreadContextSwitch.Descriptor old = _slots[index].Context;
        return captured.Identity == old.Identity && captured.CurrentIdentity == old.Identity &&
            captured.Context == old.Context && captured.ContextBytes == old.ContextBytes &&
            captured.StackGuardStart == old.StackGuardStart && captured.StackBytes == old.StackBytes &&
            captured.Kind == ThreadContextSwitch.ResumeKind.CapturedCall &&
            ThreadContextSwitch.AdmitResume(_switchOwner, captured) == ThreadContextSwitch.Status.Ok
            ? Status.Ok : Status.InvalidContext;
    }
    private Status Running(ulong key, ulong flags, OSHostHandle identity, out uint index)
    {
        index = 0; Status status = Access(key, flags); if (status != Status.Ok) return status;
        status = Lookup(identity, out index); if (status != Status.Ok) return status;
        return _current.Value != identity.Value ? Status.NotCurrent : _slots[index].State != State.Running ? Status.WrongState : Status.Ok;
    }
    private Status WaitSlot(KernelThreadWait wait, out uint index)
    {
        Status status = Lookup(wait.Thread, out index); if (status != Status.Ok) return status;
        return !wait.IsValid || _slots[index].Sequence != wait.Sequence ? Status.StaleWait : Status.Ok;
    }
    private Status Lookup(OSHostHandle identity, out uint index)
    {
        uint slot = (uint)identity.Value; index = slot - 1;
        if (slot == 0 || slot > _capacity || (uint)(identity.Value >> 32) == 0) return Status.InvalidIdentity;
        if (_slots[index].Generation != (uint)(identity.Value >> 32) ||
            _slots[index].State == State.Empty || _slots[index].State == State.Retired) return Status.StaleIdentity;
        return Status.Ok;
    }
    private Status Access(ulong key, ulong flags)
    {
        if (!_initialized) return Status.Uninitialized;
        if (key == 0 || key != _key) return Status.WrongOwner;
        fixed (KernelThreadTable* self = &this) if ((ulong)self != _self) return Status.CopiedOwner;
        return IfClear(flags) ? Status.Ok : Status.InterruptsEnabled;
    }
    private readonly OSHostHandle Id(uint index) => new(((ulong)_slots[index].Generation << 32) | (index + 1UL));
    private readonly bool SupervisorStack(ulong rsp) => Contains(_supervisor.StackGuardStart + ThreadContextSwitch.Page,
        _supervisor.StackBytes - 2UL * ThreadContextSwitch.Page, rsp, 32);
    private readonly bool ResourceOverlap(ThreadContextSwitch.Descriptor value)
    {
        if (BorrowOverlap((ulong)value.Context, value.ContextBytes) || BorrowOverlap(value.StackGuardStart, value.StackBytes) ||
            Overlap(value.StackGuardStart, value.StackBytes, _supervisor.StackGuardStart, _supervisor.StackBytes) ||
            Overlap((ulong)value.Context, value.ContextBytes, (ulong)_supervisor.Context, _supervisor.ContextBytes)) return true;
        for (uint i = 0; i < _capacity; i++)
        {
            if (_slots[i].State == State.Empty || _slots[i].State == State.Retired) continue;
            ThreadContextSwitch.Descriptor other = _slots[i].Context;
            if (Overlap((ulong)value.Context, value.ContextBytes, (ulong)other.Context, other.ContextBytes) ||
                Overlap(value.StackGuardStart, value.StackBytes, other.StackGuardStart, other.StackBytes)) return true;
        }
        return false;
    }
    private readonly bool BorrowOverlap(ulong start, ulong bytes) => Overlap(start, bytes, _self, (uint)sizeof(KernelThreadTable)) ||
        Overlap(start, bytes, (ulong)_slots, _slotBytes) || Overlap(start, bytes, (ulong)_readyStorage, _readyBytes) ||
        Overlap(start, bytes, (ulong)_deadlineStorage, _deadlineBytes);
    private static bool IfClear(ulong flags) => (flags & 0x202) == 2;
    private static bool Buffer(ulong start, ulong bytes, ulong needed) => needed != 0 && needed <= bytes && (start & 7) == 0 && Range(start, bytes);
    private static bool Range(ulong start, ulong bytes) => start != 0 && bytes != 0 && bytes - 1 <= ulong.MaxValue - start &&
        Canonical(start) && Canonical(start + bytes - 1) && ((start ^ (start + bytes - 1)) & (1UL << 47)) == 0;
    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Contains(ulong outer, ulong bytes, ulong inner, ulong count) => Range(inner, count) && inner >= outer &&
        inner - outer <= bytes && count <= bytes - (inner - outer);
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a <= b + (bn - 1) && b <= a + (an - 1);
}
