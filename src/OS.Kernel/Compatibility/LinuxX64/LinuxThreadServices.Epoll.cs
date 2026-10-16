#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Refresh actual socket readiness and wake matching retained epoll
    /// waits after the serialized network owner processes packets, ACKs or close.
    /// Call before deciding the supervisor has no runnable work. Early wake is
    /// retained by the real table; this neither transfers a CPU nor drops borrows.
    /// A failed ownership check stops further user dispatch and retains storage.</summary>
    public bool PollDescriptorReadiness(ulong key, ulong flags)
        => Access(key, flags) && !_failed && _descriptors != null && WakeEpollChanges(flags);

    private LinuxSyscallResult BeginEpoll(ulong flags, Slot* slot, LinuxSyscallFrame frame)
    {
        bool selected = LinuxEpollServices.BeginOperation(_descriptors, _self, frame, ref slot->EpollOperation, out long value);
        if (!selected) { _failed = true; return new(LinuxSyscallAction.Rejected); }
        if (!slot->EpollOperation.IsActive) return new(LinuxSyscallAction.Return, value);
        int milliseconds = unchecked((int)frame.Argument3);
        slot->WaitDeadline = 0; slot->WaitCall = frame;
        if (milliseconds > 0)
        {
            if (!_futexEnabled)
            {
                if (!DropEpollOperation(slot)) return new(LinuxSyscallAction.Rejected);
                return new(LinuxSyscallAction.Return, Unsupported);
            }
            if (!Clock(out ulong now)) { _failed = true; return new(LinuxSyscallAction.Rejected); }
            ulong duration = (ulong)milliseconds * 1_000_000;
            slot->WaitDeadline = duration > TimeEnd - now ? TimeEnd : now + duration;
        }
        return ContinueEpoll(flags, slot);
    }

    /// <summary>Commit a staged epoll wait only after CPL3 restoration and actual
    /// kernel CALL capture. Cancellation during Parking still passes through
    /// this capture before resumed exit releases its original description.</summary>
    public bool CommitEpollWait(ulong key, ulong flags, ThreadContextSwitch.Descriptor captured, out bool handoff)
    {
        handoff = false;
        if (!Access(key, flags) || _descriptors == null || !Lookup(flags, _owner.Threads->Current, out uint index)) return false;
        Slot* slot = _owner.Slots + index;
        if (!slot->EpollOperation.IsActive || !slot->Wait.IsValid || slot->WaitSource != 3) return false;
        var parked = _owner.Threads->Park(_owner.ThreadKey, flags, slot->Wait, captured);
        if (!parked.Succeeded) return false;
        handoff = parked.State == KernelThreadTable.State.Waiting; return true;
    }

    /// <summary>Consume the actual current worker's completed epoll wait. Woken
    /// readiness is retried against the original port; another waiter may have
    /// consumed it, so a fresh real wait may be staged. Deadlines return zero,
    /// signals return EINTR without restart, and shutdown stages thread exit.
    /// Output faults consume no queued events. No signal handler executes here.</summary>
    public LinuxSyscallResult CompleteEpollWait(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _descriptors == null || !Current(flags, out uint index)) return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
        if (!state.Succeeded || !slot->Wait.IsValid || slot->WaitSource != 3 || !slot->EpollOperation.IsActive ||
            slot->Wait.Sequence != state.Wait.Sequence || state.Completion == KernelThreadTable.Completion.None) return new(LinuxSyscallAction.Rejected);
        if (_stopping || slot->Exiting != 0)
        {
            if (!DropEpollOperation(slot)) return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.ThreadExited, exitCode: slot->ExitCode);
        }
        if (_failed) return new(LinuxSyscallAction.Rejected);
        if (state.Completion == KernelThreadTable.Completion.Interrupted)
        {
            if (!DropEpollOperation(slot)) return new(LinuxSyscallAction.Rejected);
            slot->Image.Value = -4; return new(LinuxSyscallAction.Return, -4);
        }
        if (state.Completion is not (KernelThreadTable.Completion.Woken or KernelThreadTable.Completion.Deadline))
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        slot->Wait = default; slot->WaitSource = 0;
        var result = ContinueEpoll(flags, slot);
        if (result.Action == LinuxSyscallAction.Return) slot->Image.Value = result.Value;
        return result;
    }

    private LinuxSyscallResult ContinueEpoll(ulong flags, Slot* slot)
    {
        ulong now = _owner.Threads->ObservedTicks;
        if (slot->WaitDeadline != 0 && (!Clock(out now) || _owner.Threads->Advance(_owner.ThreadKey, flags, now) != KernelThreadTable.Status.Ok))
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        bool expired = slot->WaitDeadline != 0 && now >= slot->WaitDeadline;
        if (!LinuxEpollServices.AttemptOperation(_self, ref slot->EpollOperation, expired, out long value, out bool blocked))
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        if (blocked)
        {
            bool timed = slot->WaitDeadline != 0;
            var begin = _owner.Threads->BeginWait(_owner.ThreadKey, flags, slot->Kernel,
                timed ? KernelThreadTable.WaitKind.Sleep : KernelThreadTable.WaitKind.Park, now, timed ? slot->WaitDeadline - now : 0);
            if (begin.Succeeded)
            {
                slot->Wait = begin.Wait; slot->WaitSource = 3;
                if (!WakeEpollChanges(flags)) return new(LinuxSyscallAction.Rejected);
                return new(LinuxSyscallAction.Blocked);
            }
            value = Again;
        }
        if (!DropEpollOperation(slot)) return new(LinuxSyscallAction.Rejected);
        return new(LinuxSyscallAction.Return, value);
    }

    private bool WakeEpollChanges(ulong flags)
    {
        if (!LinuxDescriptorServices.Attached(_descriptors, _self)) { _failed = true; return false; }
        if (_descriptors->Epoll == null) return true;
        if (!LinuxEpollServices.Refresh(_descriptors->Epoll)) { _failed = true; return false; }
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* slot = _owner.Slots + i;
            if (!slot->Wait.IsValid || slot->WaitSource != 3 || !slot->EpollOperation.IsActive) continue;
            if (!LinuxEpollServices.HasEvents(_self, ref slot->EpollOperation, out bool ready)) { _failed = true; return false; }
            if (!ready) continue;
            var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
            if (!state.Succeeded || state.Wait.Sequence != slot->Wait.Sequence) { _failed = true; return false; }
            if (state.Completion != KernelThreadTable.Completion.None) continue;
            if (!_owner.Threads->Wake(_owner.ThreadKey, flags, slot->Wait).Succeeded) { _failed = true; return false; }
        }
        return true;
    }
    private bool DropEpollOperation(Slot* slot)
    {
        if (!slot->EpollOperation.IsActive) return true;
        if (_descriptors == null || !LinuxEpollServices.EndOperation(_self, ref slot->EpollOperation)) { _failed = true; return false; }
        if (slot->WaitSource == 3) { slot->Wait = default; slot->WaitSource = 0; }
        slot->WaitDeadline = 0; slot->WaitCall = default; return true;
    }
}
#endif
