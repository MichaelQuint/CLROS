#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Trusted serialized supervisor callbacks, borrowed for this
    /// invocation only. Both must be null for one-step mode, or nonnull with a
    /// stable Context. Clock returns monotonic nanoseconds; Flush retains each
    /// step's evidence before any next step. Return exactly1; never throw,
    /// reenter or mutate thread/descriptor/FS/block ownership. No user pointers.
    /// A callback failure rejects while all original backing stays retained.</summary>
    public struct DiskPumpAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, ulong*, byte> Clock;
        public delegate* unmanaged<void*, byte> Flush;
    }

    /// <summary>Advance one of eight retained requests, including cancelled
    /// orphan records, then inspect at most16 actual worker generations. Wake
    /// only its original live source4 wait. Optional trusted callbacks permit
    /// at most4 separate steps/5ms elapsed checks, flushing every step; no
    /// progress, ready/cancelled/exiting work or budget expiry yields. A
    /// signal/deadline/cancel winner is never overwritten. This serialized
    /// IRQ-off pump never copies user bytes.</summary>
    public bool PollDiskReadiness(ulong key, ulong flags, DiskPumpAccess access = default)
    {
        if (!Access(key, flags) || _descriptors == null || !LinuxDescriptorServices.Attached(_descriptors, _self)) return false;
        if (_descriptors->Disk == null) return true;
        bool quantum = access.Clock != null || access.Flush != null;
        ulong started = 0;
        if (quantum && (access.Context == null || access.Clock == null || access.Flush == null ||
            access.Clock(access.Context, &started) != 1)) return false;
        // Four separate primitives, with an elapsed check before each extra
        // step. A primitive/flush can overshoot5ms; this is not an IRQ bound.
        for (uint step = 0; step < (quantum ? 4U : 1U); step++)
        {
            if (step != 0)
            {
                ulong now = 0;
                if (access.Clock(access.Context, &now) != 1 || now < started) return false;
                if (now - started >= 5000000UL) break;
            }
            var progress = LinuxDescriptorServices.PumpDiskOne(_descriptors, _self);
            if (progress.Progress == LinuxDescriptorServices.DiskProgress.Rejected) { _failed = true; return false; }
            bool ready = false;
            for (uint i = 0; i < _owner.Capacity; i++)
            {
                Slot* slot = _owner.Slots + i;
                if (!slot->DiskOperation.IsActive) continue;
                if (!slot->Wait.IsValid || slot->WaitSource != 4 || slot->Exiting != 0) { ready = true; continue; }
                var current = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
                if (!current.Succeeded || current.Wait.Sequence != slot->Wait.Sequence) { _failed = true; return false; }
                if (current.Completion != KernelThreadTable.Completion.None) { ready = true; continue; }
                var observed = LinuxDescriptorServices.PollDisk(_descriptors, _self, slot->DiskOperation);
                if (observed.Progress == LinuxDescriptorServices.DiskProgress.Rejected || observed.Worker.Value != slot->Kernel.Value)
                { _failed = true; return false; }
                if (observed.Progress != LinuxDescriptorServices.DiskProgress.Ready) continue;
                if (!_owner.Threads->Wake(_owner.ThreadKey, flags, slot->Wait).Succeeded) { _failed = true; return false; }
                ready = true;
            }
            if (quantum && access.Flush(access.Context) != 1) return false;
            if (ready || _owner.Threads->ReadyCount != 0 || !progress.Advanced) break;
        }
        return true;
    }

    // Called only by DispatchDescriptor after its actual current-image checks.
    // NotHandled preserves every staged/package/pipe/socket path unchanged.
    private bool TryBeginDisk(ulong flags, Slot* slot, LinuxSyscallFrame frame, out LinuxSyscallResult result)
    {
        var begun = LinuxDescriptorServices.BeginDisk(_descriptors, _self, slot->Kernel, frame);
        result = new(LinuxSyscallAction.Rejected);
        if (begun.Progress == LinuxDescriptorServices.DiskProgress.NotHandled) return false;
        if (begun.Progress == LinuxDescriptorServices.DiskProgress.Immediate)
        { result = new(LinuxSyscallAction.Return, begun.Value); return true; }
        if (begun.Progress != LinuxDescriptorServices.DiskProgress.Pending || !begun.Token.IsActive || begun.Worker.Value != slot->Kernel.Value)
        { _failed = true; return true; }
        slot->DiskOperation = begun.Token; slot->WaitCall = frame; slot->WaitDeadline = 0;
        slot->DiskOutputBytes = CaptureDiskOutputPrefix(frame);
        result = StageDiskWait(flags, slot); return true;
    }

    private LinuxSyscallResult StageDiskWait(ulong flags, Slot* slot)
    {
        var begin = _owner.Threads->BeginWait(_owner.ThreadKey, flags, slot->Kernel,
            KernelThreadTable.WaitKind.Park, _owner.Threads->ObservedTicks);
        if (!begin.Succeeded)
        {
            if (!DropDiskOperation(slot)) return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.Return, Again);
        }
        slot->Wait = begin.Wait; slot->WaitSource = 4;
        if (!InterruptPendingWait(flags, slot)) return new(LinuxSyscallAction.Rejected);
        return new(LinuxSyscallAction.Blocked);
    }

    /// <summary>Commit source4 Parking only after the native CPL3 restoration
    /// and real kernel CALL capture. Early completion stays with the exact wait
    /// sequence; an unresolved capture never permits metadata release.</summary>
    public bool CommitDiskWait(ulong key, ulong flags, ThreadContextSwitch.Descriptor captured, out bool handoff)
    {
        handoff = false;
        if (!Access(key, flags) || _descriptors == null || !Lookup(flags, _owner.Threads->Current, out uint index)) return false;
        Slot* slot = _owner.Slots + index;
        if (!slot->DiskOperation.IsActive || !slot->Wait.IsValid || slot->WaitSource != 4) return false;
        var parked = _owner.Threads->Park(_owner.ThreadKey, flags, slot->Wait, captured);
        if (!parked.Succeeded) return false;
        handoff = parked.State == KernelThreadTable.State.Waiting; return true;
    }

    /// <summary>Consume only the current original worker's won wait. The saved
    /// captured syscall supplies scalar user addresses afresh; no resolved user
    /// pointer survives a pump. CompleteDisk rechecks fd generation and mappings
    /// before its at-most8192-byte copy and cursor commit. Interrupted requests
    /// detach for orphan draining and follow the existing zero-progress restart
    /// decision; exit/cancellation never performs a late copy.</summary>
    public LinuxSyscallResult CompleteDiskWait(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _descriptors == null || !Current(flags, out uint index)) return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
        if (!state.Succeeded || !slot->Wait.IsValid || slot->WaitSource != 4 || !slot->DiskOperation.IsActive ||
            state.Wait.Sequence != slot->Wait.Sequence || state.Completion == KernelThreadTable.Completion.None)
            return new(LinuxSyscallAction.Rejected);
        if (_stopping || slot->Exiting != 0)
        {
            if (!DropDiskOperation(slot)) return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.ThreadExited, exitCode: slot->ExitCode);
        }
        if (_failed) return new(LinuxSyscallAction.Rejected);
        if (state.Completion == KernelThreadTable.Completion.Interrupted)
        {
            // No user bytes were published by Begin/Pump; preserve WaitCall for
            // the existing SA_RESTART handler predicate and original syscall RIP.
            if (!DropDiskOperation(slot)) return new(LinuxSyscallAction.Rejected);
            slot->RestartMode = 1; slot->Image.Value = -4;
            return new(LinuxSyscallAction.Return, -4);
        }
        if (state.Completion != KernelThreadTable.Completion.Woken) { _failed = true; return new(LinuxSyscallAction.Rejected); }
        slot->Wait = default; slot->WaitSource = 0;
        var completed = LinuxDescriptorServices.CompleteDisk(_descriptors, _self, slot->Kernel, slot->DiskOperation, slot->WaitCall, slot->DiskOutputBytes);
        if (completed.Progress == LinuxDescriptorServices.DiskProgress.Immediate ||
            completed.Progress == LinuxDescriptorServices.DiskProgress.Draining && !completed.Worker.IsValid)
        {
            // A failed copy/close may leave an owner-independent quarantine
            // record. The worker returns its real error; native pumping owns it.
            slot->DiskOperation = default; slot->DiskOutputBytes = 0; slot->WaitCall = default; slot->WaitDeadline = 0;
            slot->Image.Value = completed.Value; return new(LinuxSyscallAction.Return, completed.Value);
        }
        if (completed.Progress is LinuxDescriptorServices.DiskProgress.Pending or LinuxDescriptorServices.DiskProgress.Draining)
            return StageDiskWait(flags, slot);
        _failed = true; return new(LinuxSyscallAction.Rejected);
    }

    // Snapshot only the original contiguous writable prefix, never a resolved
    // alias. At most3 pages cover an unaligned8KiB read. The completion ceiling
    // preserves zero-byte EOF and valid short reads with an inaccessible tail;
    // mapping a formerly absent tail later cannot enlarge copy authority.
    private uint CaptureDiskOutputPrefix(LinuxSyscallFrame call)
    {
        uint maximum = call.Number is 4 or 6 ? 144U : call.Number is 0 or 17 ?
            (uint)(call.Argument2 < LinuxDescriptorServices.DiskCompletionBytes ? call.Argument2 : LinuxDescriptorServices.DiskCompletionBytes) : 0;
        ulong address = call.Argument1; uint admitted = 0;
        while (admitted < maximum)
        {
            if (address < 4096 || address >= LinuxSyscallGateway.UserEnd) break;
            uint bytes = 4096U - (uint)(address & 4095);
            if (bytes > maximum - admitted) bytes = maximum - admitted;
            if (!LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Write, _owner.User)) break;
            admitted += bytes; address += bytes;
        }
        return admitted;
    }

    // Consulted by the same memory-owner callback as futex backing. Protect
    // Parking, Waiting, Ready and completed-but-unconsumed work, including a
    // close/error whose final result has not revoked the original copy yet.
    // CancelDisk revokes copy before DropDiskOperation clears this ceiling.
    // Other workers may change every nonoverlapping interval. No live device
    // dereference is needed, so a reset/failure cannot silently remove the pin.
    private bool MayChangeDiskOutput(ulong address, ulong bytes)
    {
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* slot = _owner.Slots + i;
            if (!slot->DiskOperation.IsActive) continue;
            var call = slot->WaitCall;
            if (call.Number is not (0 or 2 or 4 or 6 or 8 or 17 or 257) ||
                slot->DiskOutputBytes > LinuxDescriptorServices.DiskCompletionBytes ||
                slot->DiskOutputBytes != 0 && !UserExtent(call.Argument1, slot->DiskOutputBytes)) return false;
            if (slot->DiskOutputBytes != 0 && Overlap(address, bytes, call.Argument1, slot->DiskOutputBytes)) return false;
        }
        return true;
    }

    private bool DropDiskOperation(Slot* slot)
    {
        if (!slot->DiskOperation.IsActive) return true;
        if (_descriptors == null || !LinuxDescriptorServices.Attached(_descriptors, _self)) { _failed = true; return false; }
        var cancelled = LinuxDescriptorServices.CancelDisk(_descriptors, _self, slot->DiskOperation);
        if (cancelled.Progress is not (LinuxDescriptorServices.DiskProgress.Immediate or LinuxDescriptorServices.DiskProgress.Draining))
        { _failed = true; return false; }
        slot->DiskOperation = default; slot->DiskOutputBytes = 0;
        if (slot->WaitSource == 4) { slot->Wait = default; slot->WaitSource = 0; }
        // WaitCall is still required by the signal restart decision until the
        // current owner either delivers a handler or commits the real exit.
        slot->WaitDeadline = 0; return true;
    }
}
#endif
