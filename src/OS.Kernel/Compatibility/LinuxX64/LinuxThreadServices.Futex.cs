using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Exclusive kernel bookkeeping for one address/mode/backing
    /// lifetime. References include completed waits until their actual worker
    /// consumes completion. Never copy/modify a bound cell or free its alias.</summary>
    public struct FutexCell
    {
        internal ulong Address, Lifetime;
        internal uint* Alias;
        internal uint Mode, References;
        internal WaitAddress.Binding Binding;
    }

    /// <summary>Mapped, disjoint caller-owned storage and genuine user-memory /
    /// monotonic-clock capabilities, used with User.Context. Resolve borrows the
    /// exact readable four-byte alias with a nonzero, nonreused mapping lifetime.
    /// It changes no mappings/ownership. Distinct user words need distinct
    /// aliases. Only this process's anonymous/private image backing is supported;
    /// user-VA aliases and file/shared-memory mappings are unsupported. Shared
    /// anonymous operations require writable backing; private waits need read.
    /// The memory owner must consult MayChangeUserRange before every overlapping
    /// unmap, fixed replacement, protection, discard or heap shrink. Keep aliases
    /// mapped/exclusive until completion drops every reference. The kernel table
    /// must use monotonic nanoseconds, never APIC count or diagnostic IRQ epochs.
    /// ReadMonotonic supplies the actual owned clock; these declarations and host
    /// simulations are not hardware/lifetime proof.</summary>
    public struct FutexAdmission
    {
        public FutexCell* Cells;
        public WaitAddress.Slot* Addresses;
        public WaitAddress.Registration* Registrations;
        public uint Capacity, CellBytes, AddressBytes, RegistrationBytes;
        public ulong OwnerKey;
        public delegate* unmanaged<void*, ulong, uint**, ulong*, byte> Resolve;
        public delegate* unmanaged<void*, ulong*, byte> ReadMonotonic;
    }

    private const long TimedOut = -110;
    private const ulong TimeEnd = long.MaxValue;

    /// <summary>Enable this process's futex owner before Start. All admission
    /// checks precede writes; storage remains borrowed until process Release.
    /// This initializes the existing WaitAddress owner, never a second scheduler.</summary>
    public bool EnableFutex(ulong key, ulong flags, FutexAdmission a)
    {
        if (!Access(key, flags) || _futexEnabled || _failed || _stopping || _active != 0 ||
            a.OwnerKey == 0 || a.Resolve == null || a.ReadMonotonic == null || a.Capacity == 0 || a.Capacity > WaitAddress.Maximum ||
            !Buffer((ulong)a.Cells, a.CellBytes, a.Capacity * (uint)sizeof(FutexCell)) ||
            !Buffer((ulong)a.Addresses, a.AddressBytes, a.Capacity * (uint)sizeof(WaitAddress.Slot)) ||
            !Buffer((ulong)a.Registrations, a.RegistrationBytes, _owner.Capacity * (uint)sizeof(WaitAddress.Registration))) return false;
        ulong* starts = stackalloc ulong[3] { (ulong)a.Cells, (ulong)a.Addresses, (ulong)a.Registrations };
        ulong* sizes = stackalloc ulong[3] { a.CellBytes, a.AddressBytes, a.RegistrationBytes };
        for (uint i = 0; i < 3; i++)
        {
            if (BorrowOverlap(starts[i], sizes[i]) || DescriptorStorageOverlap(starts[i], sizes[i])) return false;
            for (uint j = 0; j < i; j++) if (Overlap(starts[i], sizes[i], starts[j], sizes[j])) return false;
            for (uint j = 0; j < _owner.Capacity; j++)
            {
                var d = _owner.Workers[j].Initial;
                if (Overlap(starts[i], sizes[i], (ulong)d.Context, d.ContextBytes) || Overlap(starts[i], sizes[i], d.StackGuardStart, d.StackBytes)) return false;
            }
        }
        for (uint i = 0; i < a.Capacity * sizeof(FutexCell); i++) if (((byte*)a.Cells)[i] != 0) return false;
        if (_addresses.Initialize(a.OwnerKey, flags, _owner.Threads, _owner.ThreadKey, a.Addresses, a.AddressBytes,
            a.Capacity, a.Registrations, a.RegistrationBytes, _owner.Capacity) != WaitAddress.Status.Ok) return false;
        _futex = a; _futexEnabled = true; return true;
    }

    /// <summary>Admission guard for the actual user-memory owner. False means
    /// unauthorized/invalid input or a retained wait intersects the mutation.
    /// Pass the complete affected extent (including rounded/discarded pages).
    /// Completed but unconsumed waits still protect their exact backing.</summary>
    public bool MayChangeUserRange(ulong key, ulong flags, ulong address, ulong bytes)
    {
        if (!Access(key, flags) || !UserExtent(address, bytes)) return false;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (!MayChangeDiskOutput(address, bytes)) return false;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!MayChangeWritableDiskOutput(address, bytes)) return false;
#endif
        if (_futexEnabled) for (uint i = 0; i < _futex.Capacity; i++)
            if (_futex.Cells[i].References != 0 && Overlap(address, bytes, _futex.Cells[i].Address, 4)) return false;
        return true;
    }

    /// <summary>Read the real monotonic capability and advance the same kernel
    /// table's nanosecond deadlines. The native IRQ owner separately acknowledges
    /// hardware and drains/dispatches ready workers. No fabricated tick or native
    /// transfer occurs here; a failed/regressing clock changes no scheduler time.</summary>
    public bool AdvanceFutexClock(ulong key, ulong flags) => Access(key, flags) && _futexEnabled &&
        Clock(out ulong now) && _owner.Threads->Advance(_owner.ThreadKey, flags, now) == KernelThreadTable.Status.Ok;

    /// <summary>Finish a previously staged futex wait after the complete CPL3
    /// envelope restores, using an actual kernel CALL capture. An earlier deadline
    /// can finish in place; otherwise the native owner must transfer and confirm
    /// the supervisor. Failure retains the staged wait and its backing.</summary>
    public bool CommitFutexWait(ulong key, ulong flags, ThreadContextSwitch.Descriptor captured, out bool handoff)
    {
        handoff = false;
        if (!Access(key, flags) || !_futexEnabled || !Lookup(flags, _owner.Threads->Current, out uint index)) return false;
        Slot* slot = _owner.Slots + index; if (!slot->Wait.IsValid || slot->WaitSource != 1) return false;
        var parked = _owner.Threads->Park(_owner.ThreadKey, flags, slot->Wait, captured);
        if (!parked.Succeeded) return false;
        handoff = parked.State == KernelThreadTable.State.Waiting; return true;
    }

    /// <summary>Consume exactly the current actual worker's completed wait before
    /// installing/resuming its user image. Wake returns zero; deadline returns
    /// ETIMEDOUT. Interrupted drops the word borrow and stages an EINTR/restart
    /// decision that withholds CurrentImage until pending delivery resolves it.
    /// Shutdown cancellation remains a staged thread exit. References drop only
    /// after validated completion; a timed restart retains an absolute deadline.</summary>
    public LinuxSyscallResult CompleteFutexWait(ulong key, ulong flags)
    {
        if (!Access(key, flags) || !_futexEnabled || !Current(flags, out uint index)) return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
        if (!slot->Wait.IsValid || slot->WaitSource != 1 || slot->Wait.Sequence != state.Wait.Sequence || state.Completion == KernelThreadTable.Completion.None)
            return new(LinuxSyscallAction.Rejected);
        if (!DropFutexWait(flags, slot)) return new(LinuxSyscallAction.Rejected);
        if (_stopping || slot->Exiting != 0) return new(LinuxSyscallAction.ThreadExited, exitCode: slot->ExitCode);
        if (_failed) return new(LinuxSyscallAction.Rejected);
        if (state.Completion == KernelThreadTable.Completion.Interrupted)
        {
            slot->RestartMode = slot->WaitCall.Argument3 == 0 ? 1U : 2;
            if (slot->RestartMode == 2)
            {
                slot->TimedRestartCall = slot->WaitCall; slot->TimedRestartDeadline = slot->WaitDeadline;
                slot->TimedRestartArmed = true;
            }
            slot->Image.Value = -4; // Only EINTR is exposed; restart policy stays kernel metadata.
            return new(LinuxSyscallAction.Return, -4);
        }
        long value = state.Completion == KernelThreadTable.Completion.Woken ? 0 : TimedOut;
        if (state.Completion is not (KernelThreadTable.Completion.Woken or KernelThreadTable.Completion.Deadline))
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        slot->Image.Value = value; return new(LinuxSyscallAction.Return, value);
    }

    private LinuxSyscallResult Futex(ulong flags, Slot* slot, LinuxSyscallFrame frame, bool restarted = false, ulong deadline = 0)
    {
        if (!_futexEnabled) return new(LinuxSyscallAction.Return, Unsupported);
        uint op = unchecked((uint)frame.Argument1), mode = op & 128, cmd = op & ~384U;
        if (cmd is not (0 or 1 or 3 or 9 or 10)) return new(LinuxSyscallAction.Return, Unsupported);
        if (cmd == 3) return (op & 256) != 0 ? new(LinuxSyscallAction.Return, Unsupported) : FutexRequeue(flags, frame, mode);
        bool wait = cmd is 0 or 9, timed = wait && (restarted || frame.Argument3 != 0);
        ulong duration = 0;
        if (timed && !restarted)
        {
            long* ts = stackalloc long[2];
            if (!Input(frame.Argument3, (byte*)ts, 16)) return new(LinuxSyscallAction.Return, Fault);
            if (ts[0] < 0 || ts[1] < 0 || ts[1] >= 1_000_000_000) return new(LinuxSyscallAction.Return, Invalid);
            ulong seconds = (ulong)ts[0], nanos = (ulong)ts[1];
            duration = seconds > TimeEnd / 1_000_000_000 || seconds == TimeEnd / 1_000_000_000 && nanos > TimeEnd % 1_000_000_000
                ? TimeEnd : seconds * 1_000_000_000 + nanos;
        }
        if ((op & 256) != 0) return new(LinuxSyscallAction.Return, Unsupported); // No realtime clock is admitted.
        uint bits = cmd is 0 or 1 ? uint.MaxValue : unchecked((uint)frame.Argument5);
        if (bits == 0 || (frame.Argument0 & 3) != 0) return new(LinuxSyscallAction.Return, Invalid);
        if (!UserExtent(frame.Argument0, 4)) return new(LinuxSyscallAction.Return, Fault);
        if (!wait)
        {
            // Legacy futex wakes at least one matching waiter even for a zero
            // or negative int count; FUTEX_STRICT is outside this ABI subset.
            int count = unchecked((int)frame.Argument2);
            long value = FutexWake(flags, frame.Argument0, mode, count <= 0 ? 1U : (uint)count, bits);
            return _failed ? new(LinuxSyscallAction.Rejected) : new(LinuxSyscallAction.Return, value);
        }
        if (!Clock(out ulong now)) return new(LinuxSyscallAction.Rejected);
        ulong delay = !timed ? 0 : restarted ? (deadline > now ? deadline - now : 0) :
            cmd == 0 ? (duration > TimeEnd - now ? TimeEnd - now : duration) : duration > now ? duration - now : 0;
        long error = ResolveFutex(flags, frame.Argument0, mode, true, out uint cellIndex);
        if (error != 0) return _failed ? new(LinuxSyscallAction.Rejected) : new(LinuxSyscallAction.Return, error);
        FutexCell* cell = _futex.Cells + cellIndex;
        var begin = _addresses.Begin(_futex.OwnerKey, flags, cell->Binding, slot->Kernel, unchecked((uint)frame.Argument2), now, timed, delay, bits);
        if (!begin.Succeeded)
        {
            if (cell->References == 0 && !UnbindFutexCell(flags, cellIndex)) return new(LinuxSyscallAction.Rejected);
            error = begin.Code == WaitAddress.Status.ValueChanged ? Again : begin.Code == WaitAddress.Status.TimedOut ? TimedOut : Again;
            if (begin.Code == WaitAddress.Status.ThreadFailure && begin.ThreadStatus is not (KernelThreadTable.Status.DeadlineFull or KernelThreadTable.Status.IdentityExhausted))
            { _failed = true; return new(LinuxSyscallAction.Rejected); }
            return new(LinuxSyscallAction.Return, error);
        }
        cell->References++; slot->Wait = begin.Wait; slot->FutexCellIndex = cellIndex; slot->WaitSource = 1;
        slot->WaitCall = frame; slot->WaitDeadline = timed ? now + delay : 0;
        if (!InterruptPendingWait(flags, slot)) return new(LinuxSyscallAction.Rejected);
        return new(LinuxSyscallAction.Blocked);
    }

    private long FutexWake(ulong flags, ulong address, uint mode, uint count, uint bits)
    {
        if ((address & 3) != 0) return Invalid;
        if (!UserExtent(address, 4)) return Fault;
        long error = ResolveFutex(flags, address, mode, false, out uint index);
        if (error != 0) return error;
        if (index == _futex.Capacity) return 0;
        var wake = _addresses.Wake(_futex.OwnerKey, flags, _futex.Cells[index].Binding, count, bits);
        if (!wake.Succeeded) { _failed = true; return Io; }
        return wake.Count;
    }

    private long ResolveFutex(ulong flags, ulong address, uint mode, bool create, out uint index)
    {
        index = _futex.Capacity; uint free = _futex.Capacity;
        for (uint i = 0; i < _futex.Capacity; i++)
        {
            if (_futex.Cells[i].Binding.IsValid && _futex.Cells[i].Address == address && _futex.Cells[i].Mode == mode) index = i;
            if (!_futex.Cells[i].Binding.IsValid && free == _futex.Capacity) free = i;
        }
        // Private WAKE needs only a numerical userspace key if nobody retains it.
        if (!create && mode == 128 && index == _futex.Capacity) return 0;
        uint* alias = null; ulong lifetime = 0;
        var required = mode == 0 ? LinuxUserPageAccess.Read | LinuxUserPageAccess.Write : LinuxUserPageAccess.Read;
        if (!LinuxSyscallGateway.ValidateRange(address, 4, required, _owner.User) ||
            _futex.Resolve(_owner.User.Context, address, &alias, &lifetime) != 1 || lifetime == 0 ||
            ((ulong)alias & 3) != 0 || !Range((ulong)alias, 4) || ForbiddenFutexAlias((ulong)alias))
        { if (index != _futex.Capacity) _failed = true; return Fault; }
        if (index != _futex.Capacity)
        {
            if (_futex.Cells[index].Alias != alias || _futex.Cells[index].Lifetime != lifetime) { _failed = true; return Io; }
            return 0;
        }
        if (!create) return 0;
        if (free == _futex.Capacity) return Again;
        var bound = _addresses.Bind(_futex.OwnerKey, flags, alias, lifetime, mode);
        if (!bound.Succeeded) return bound.Code == WaitAddress.Status.Full ? Again : Fault;
        _futex.Cells[free] = new() { Address = address, Alias = alias, Lifetime = lifetime, Mode = mode, Binding = bound.Address };
        index = free; return 0;
    }

    private bool DropFutexWait(ulong flags, Slot* slot)
    {
        if (!slot->Wait.IsValid) return true;
        if (slot->WaitSource != 1) { _failed = true; return false; }
        uint index = slot->FutexCellIndex;
        if (!_futexEnabled || index >= _futex.Capacity || _futex.Cells[index].References == 0 ||
            !_addresses.Sweep(_futex.OwnerKey, flags).Succeeded) { _failed = true; return false; }
        if (_futex.Cells[index].References == 1 && !UnbindFutexCell(flags, index)) return false;
        else if (_futex.Cells[index].References > 1) _futex.Cells[index].References--;
        slot->Wait = default; slot->FutexCellIndex = 0; slot->WaitSource = 0; return true;
    }
    private bool UnbindFutexCell(ulong flags, uint index)
    {
        if (_addresses.Unbind(_futex.OwnerKey, flags, _futex.Cells[index].Binding) != WaitAddress.Status.Ok)
        { _failed = true; return false; }
        _futex.Cells[index] = default; return true;
    }
    private bool ReleaseFutex(ulong flags)
    {
        if (!_futexEnabled) return true;
        for (uint i = 0; i < _futex.Capacity; i++) if (_futex.Cells[i].References != 0) return false;
        for (uint i = 0; i < _futex.Capacity; i++) if (_futex.Cells[i].Binding.IsValid && !UnbindFutexCell(flags, i)) return false;
        return _addresses.Release(_futex.OwnerKey, flags) == WaitAddress.Status.Ok;
    }
    private bool Clock(out ulong now)
    {
        ulong value = 0; bool valid = _futex.ReadMonotonic(_owner.User.Context, &value) == 1 && value <= TimeEnd && value >= _owner.Threads->ObservedTicks;
        now = value; return valid;
    }
    private bool FutexStorageOverlap(ulong start, ulong bytes) => Overlap(start, bytes, (ulong)_futex.Cells, _futex.CellBytes) ||
        Overlap(start, bytes, (ulong)_futex.Addresses, _futex.AddressBytes) || Overlap(start, bytes, (ulong)_futex.Registrations, _futex.RegistrationBytes);
    private bool ForbiddenFutexAlias(ulong start)
    {
        if (BorrowOverlap(start, 4) || FutexStorageOverlap(start, 4) || DescriptorStorageOverlap(start, 4, includeUserBacking: false)) return true;
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            var d = _owner.Workers[i].Initial;
            if (Overlap(start, 4, (ulong)d.Context, d.ContextBytes) || Overlap(start, 4, d.StackGuardStart, d.StackBytes)) return true;
        }
        return false;
    }
    private static bool UserExtent(ulong address, ulong bytes) => address >= 4096 && address < LinuxSyscallGateway.UserEnd &&
        bytes != 0 && bytes <= LinuxSyscallGateway.UserEnd - address;
}
