using OS.Kernel.Threading;
using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Attach the exclusive descriptor owner before Start. All user-copy
    /// capabilities must match this process. The mechanical slice requires an
    /// explicitly ignored SIGPIPE; actual signal delivery belongs to its owner.
    /// Every descriptor mutation must then pass DispatchDescriptor so actual
    /// I/O/peer-close events can wake retained waits. Raw dispatch/End reject
    /// until all threads drain and Release detaches. No native execution occurs.</summary>
    public bool EnablePipes(ulong key, ulong flags, LinuxDescriptorServices.State* descriptors)
    {
        if (!Access(key, flags) || _failed || _stopping || _active != 0 || _descriptors != null ||
            !Buffer((ulong)descriptors, (uint)sizeof(LinuxDescriptorServices.State), (uint)sizeof(LinuxDescriptorServices.State)) ||
            !LinuxDescriptorServices.Audit(descriptors)) return false;
        var a = descriptors->Owner;
        // These are exact native capability addresses, not logical method equality.
        if (!a.SigpipeIgnored || a.PackageProcessId != 0 && a.PackageProcessId != _owner.ProcessId ||
            a.User.Context != _owner.User.Context || (ulong)a.User.PageAccess != (ulong)_owner.User.PageAccess ||
            (ulong)a.User.CopyFromUser != (ulong)_owner.User.CopyFromUser || (ulong)a.CopyToUser != (ulong)_owner.CopyToUser) return false;
        for (uint i = 0; i < DescriptorExtentCount; i++)
        {
            DescriptorExtent(descriptors, i, out ulong start, out ulong bytes);
            if (bytes == 0) continue;
            if (BorrowOverlap(start, bytes) || _futexEnabled && FutexStorageOverlap(start, bytes)) return false;
            for (uint j = 0; j < _owner.Capacity; j++)
            {
                var d = _owner.Workers[j].Initial;
                if (Overlap(start, bytes, (ulong)d.Context, d.ContextBytes) || Overlap(start, bytes, d.StackGuardStart, d.StackBytes)) return false;
            }
        }
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!WritableDiskStorageOutsideProcess(descriptors, flags)) return false;
#endif
        if (!LinuxDescriptorServices.AttachThreads(descriptors, _self)) return false;
        _descriptors = descriptors; return true;
    }

    /// <summary>Execute descriptor calls on the actual current user worker.
    /// Blocking read/write pins the original open description, releases scratch
    /// and stages a real table wait. Nonblocking and other descriptor calls keep
    /// their existing behavior; blocking pipe writev is unsupported in this
    /// slice. The native gateway/worker owner must route this explicit result and
    /// restore CPL3 state before CommitPipeWait; no native transfer is done here.</summary>
    public LinuxSyscallResult DispatchDescriptor(ulong key, ulong flags, LinuxSyscallFrame frame)
    {
        if (!Access(key, flags) || _failed || _stopping || !Current(flags, out uint index) ||
            !LinuxSyscallGateway.ValidateReturn(frame, _owner.User) || _descriptors == null) return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        if (slot->Exiting != 0 || slot->RestartMode is 1 or 2 || slot->Wait.IsValid || HasDescriptorOperation(slot)) return new(LinuxSyscallAction.Rejected);
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (frame.Number is 232 or 281)
        {
            if (!LinuxDescriptorServices.Attached(_descriptors, _self)) { _failed = true; return new(LinuxSyscallAction.Rejected); }
            if (_descriptors->Epoll != null) return BeginEpoll(flags, slot, frame);
        }
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (TryBeginWritableDisk(flags, slot, frame, out var writableResult)) return writableResult;
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (TryBeginDisk(flags, slot, frame, out var diskResult)) return diskResult;
#endif
        uint* before = stackalloc uint[(int)LinuxDescriptorServices.MaximumPipes * 3];
        if (!LinuxDescriptorServices.DispatchThreads(_descriptors, _self, frame, ref slot->PipeOperation, before, out bool pipe, out long value))
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        if (pipe)
        {
            if (slot->PipeOperation.IsActive) return ContinuePipe(flags, slot);
            return new(LinuxSyscallAction.Return, value);
        }
        if (!WakePipeChanges(flags, before)) return new(LinuxSyscallAction.Rejected);
        return new(LinuxSyscallAction.Return, value);
    }

    /// <summary>Park a staged pipe operation only after full CPL3 restoration
    /// and actual kernel CALL capture. Early peer/I/O events are retained by
    /// the table and may complete in place. Otherwise transfer/confirm the real
    /// supervisor before another dispatch; failure retains all ownership.</summary>
    public bool CommitPipeWait(ulong key, ulong flags, ThreadContextSwitch.Descriptor captured, out bool handoff)
    {
        handoff = false;
        if (!Access(key, flags) || _descriptors == null || !Lookup(flags, _owner.Threads->Current, out uint index)) return false;
        Slot* slot = _owner.Slots + index;
        if (!slot->PipeOperation.IsActive || !slot->Wait.IsValid || slot->WaitSource != 2) return false;
        var parked = _owner.Threads->Park(_owner.ThreadKey, flags, slot->Wait, captured);
        if (!parked.Succeeded) return false;
        handoff = parked.State == KernelThreadTable.State.Waiting; return true;
    }

    /// <summary>Consume one actual completed pipe wait. Retry the original open
    /// description, never a reused fd. Another reader/writer may have consumed
    /// readiness, so retry can stage a fresh wait; large writes retain progress.
    /// Interrupted completion releases the original description, returning a
    /// transferred prefix or staging a zero-progress EINTR/restart decision.
    /// Shutdown drains as thread exit. No handler executes in this method.</summary>
    public LinuxSyscallResult CompletePipeWait(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _descriptors == null || !Current(flags, out uint index)) return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
        if (!slot->Wait.IsValid || slot->WaitSource != 2 || !slot->PipeOperation.IsActive ||
            slot->Wait.Sequence != state.Wait.Sequence || state.Completion == KernelThreadTable.Completion.None) return new(LinuxSyscallAction.Rejected);
        if (_stopping || slot->Exiting != 0)
        {
            if (!DropPipeOperation(flags, slot)) return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.ThreadExited, exitCode: slot->ExitCode);
        }
        if (_failed) return new(LinuxSyscallAction.Rejected);
        if (state.Completion == KernelThreadTable.Completion.Interrupted)
        {
            uint progress = slot->PipeOperation.TransferredBytes;
            if (!DropPipeOperation(flags, slot)) return new(LinuxSyscallAction.Rejected);
            // A transferred prefix is final: never replay it through a restart.
            slot->RestartMode = progress == 0 ? 1U : 0;
            slot->Image.Value = progress == 0 ? -4 : progress;
            return new(LinuxSyscallAction.Return, slot->Image.Value);
        }
        if (state.Completion != KernelThreadTable.Completion.Woken) { _failed = true; return new(LinuxSyscallAction.Rejected); }
        slot->Wait = default; slot->WaitSource = 0;
        var result = ContinuePipe(flags, slot);
        if (result.Action == LinuxSyscallAction.Return) slot->Image.Value = result.Value;
        return result;
    }

    private LinuxSyscallResult ContinuePipe(ulong flags, Slot* slot)
    {
        uint* before = stackalloc uint[12]; PipeSnapshot(before);
        if (!LinuxDescriptorServices.AttemptPipe(_descriptors, _self, ref slot->PipeOperation, out long value, out bool blocked))
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        if (blocked)
        {
            var begin = _owner.Threads->BeginWait(_owner.ThreadKey, flags, slot->Kernel, KernelThreadTable.WaitKind.Park, _owner.Threads->ObservedTicks);
            if (begin.Succeeded)
            {
                slot->Wait = begin.Wait; slot->WaitSource = 2;
                slot->WaitCall = slot->Image.Frame; slot->WaitDeadline = 0;
                if (!WakePipeChanges(flags, before) || !InterruptPendingWait(flags, slot)) return new(LinuxSyscallAction.Rejected);
                return new(LinuxSyscallAction.Blocked);
            }
            value = slot->PipeOperation.TransferredBytes != 0 ? slot->PipeOperation.TransferredBytes : Again;
        }
        if (!LinuxDescriptorServices.EndPipe(_descriptors, _self, ref slot->PipeOperation)) { _failed = true; return new(LinuxSyscallAction.Rejected); }
        if (!WakePipeChanges(flags, before)) return new(LinuxSyscallAction.Rejected);
        return new(LinuxSyscallAction.Return, value);
    }

    private bool DropPipeOperation(ulong flags, Slot* slot)
    {
        if (!slot->PipeOperation.IsActive) return true;
        if (_descriptors == null || !LinuxDescriptorServices.Attached(_descriptors, _self)) { _failed = true; return false; }
        uint* before = stackalloc uint[12]; PipeSnapshot(before);
        if (!LinuxDescriptorServices.EndPipe(_descriptors, _self, ref slot->PipeOperation)) { _failed = true; return false; }
        if (slot->WaitSource == 2) { slot->Wait = default; slot->WaitSource = 0; }
        return WakePipeChanges(flags, before);
    }

    private void PipeSnapshot(uint* counts)
    {
        var a = _descriptors->Owner;
        for (uint i = 0; i < a.PipeCount; i++)
        { counts[i * 3] = a.Pipes[i].BufferedBytes; counts[i * 3 + 1] = a.Pipes[i].ReaderCount; counts[i * 3 + 2] = a.Pipes[i].WriterCount; }
    }
    private bool WakePipeChanges(ulong flags, uint* before)
    {
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (!LinuxEpollServices.ThreadRefresh.Begin(_descriptors, _self, out var refresh)) { _failed = true; return false; }
#else
        if (!LinuxDescriptorServices.Attached(_descriptors, _self)) { _failed = true; return false; }
#endif
        var a = _descriptors->Owner;
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* slot = _owner.Slots + i;
            if (!slot->Wait.IsValid || slot->WaitSource != 2 || !slot->PipeOperation.IsActive) continue;
            uint p = slot->PipeOperation.Pipe;
            if (p >= a.PipeCount) { _failed = true; return false; }
            bool changed = slot->PipeOperation.Write
                ? a.Pipes[p].BufferedBytes < before[p * 3] || a.Pipes[p].ReaderCount < before[p * 3 + 1]
                : a.Pipes[p].BufferedBytes > before[p * 3] || a.Pipes[p].WriterCount < before[p * 3 + 2];
            if (!changed) continue;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
            // Inspection may enter the thread owner: do not retain admission.
            refresh = default;
#endif
            var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
            if (!state.Succeeded || state.Wait.Sequence != slot->Wait.Sequence) { _failed = true; return false; }
            if (state.Completion != KernelThreadTable.Completion.None) continue;
            if (!_owner.Threads->Wake(_owner.ThreadKey, flags, slot->Wait).Succeeded) { _failed = true; return false; }
        }
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        return WakeEpollChanges(flags, ref refresh);
#else
        return true;
#endif
    }

    private bool DescriptorStorageOverlap(ulong start, ulong bytes, bool includeUserBacking = true)
    {
        if (_descriptors == null) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!DescriptorTreesOutsideRange(_descriptors, start, bytes, includeUserBacking)) return true;
#endif
        for (uint i = 0; i < DescriptorExtentCount; i++)
        {
            // Validated futex words may use application backing; service metadata may not.
            if (i == 8 && !includeUserBacking) continue;
            DescriptorExtent(_descriptors, i, out ulong b, out ulong n);
            if (n != 0 && Overlap(start, bytes, b, n)) return true;
        }
        return false;
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
    // Do not enter Access/Current/global Audit from these leaves. Socket Audit
    // and Network Shape remain separate from their active entry gates.
    private static bool FiniteDescriptorTree(LinuxDescriptorServices.State* d)
    {
        if (d->WritableDisk != null) return true;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (d->Disk != null)
        {
            var disk = d->Disk;
            if (!Range((ulong)disk, (ulong)sizeof(LinuxDescriptorServices.DiskState)) ||
                !Range((ulong)disk->Mount, (ulong)sizeof(OS.Storage.ReadOnlyWebRootFileSystem.Mount))) return true;
            return disk->Mount->ReadPort != null;
        }
#endif
        return false;
    }
    private static bool DescriptorTreesOutsideRange(LinuxDescriptorServices.State* d, ulong start, ulong bytes, bool includeUserBacking = true)
    {
        if (!LinuxDescriptorServices.WritableDiskCanRetainStorage(d, start, bytes, includeUserBacking)) return false;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (!LinuxDescriptorServices.DiskThreadStorageOutside(d, start, bytes)) return false;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (FiniteDescriptorTree(d) && !LinuxDescriptorServices.SocketCanRetainStorage(d, start, bytes)) return false;
#endif
        return true;
    }
    private bool ProcessRetainedShape(LinuxDescriptorServices.State* d, out uint kernelCount)
    {
        kernelCount = 0;
        if (_initialized != 1 || !Range(_self, (ulong)sizeof(LinuxThreadServices)) ||
            !Range((ulong)d, (ulong)sizeof(LinuxDescriptorServices.State)) || d->Initialized != 1 ||
            (_descriptors == null ? d->ThreadOwner != 0 : _descriptors != d || d->ThreadOwner != _self) ||
            _owner.Capacity is 0 or > MaximumThreads ||
            !Buffer((ulong)_owner.Threads, (uint)sizeof(KernelThreadTable), (uint)sizeof(KernelThreadTable)) ||
            !Buffer((ulong)_owner.Slots, _owner.SlotBytes, _owner.Capacity * (uint)sizeof(Slot)) ||
            !Buffer((ulong)_owner.Workers, _owner.WorkerBytes, _owner.Capacity * (uint)sizeof(Worker)) ||
            ((ulong)_owner.User.Context & 7) != 0 || !Range((ulong)_owner.User.Context, _owner.UserContextBytes)) return false;
        fixed (LinuxThreadServices* self = &this) if ((ulong)self != _self) return false;
        var a = d->Owner;
        if (a.User.Context != _owner.User.Context || (ulong)a.User.PageAccess != (ulong)_owner.User.PageAccess ||
            (ulong)a.User.CopyFromUser != (ulong)_owner.User.CopyFromUser || (ulong)a.CopyToUser != (ulong)_owner.CopyToUser) return false;
        return _owner.Threads->InspectRetainedStorage(_owner.ThreadKey, 0, out _, out _, out kernelCount) == KernelThreadTable.Status.Ok &&
            kernelCount is >= 9 and <= 9 + 2 * KernelThreadTable.MaximumSlots;
    }
    private bool ProcessExtentAt(uint index, uint kernelCount, out ulong start, out ulong bytes)
    {
        start = bytes = 0;
        if (index >= 8 + kernelCount + 2 * _owner.Capacity) return false;
        switch (index)
        {
            case 0: start = _self; bytes = (ulong)sizeof(LinuxThreadServices); break;
            case 1: start = (ulong)_owner.Slots; bytes = _owner.SlotBytes; break;
            case 2: start = (ulong)_owner.Workers; bytes = _owner.WorkerBytes; break;
            case 3: start = (ulong)_owner.User.Context; bytes = _owner.UserContextBytes; break;
            case 4: start = (ulong)_signals; bytes = _signals == null ? 0 : (ulong)sizeof(LinuxSignalServices.State); break;
            case 5: if (_futexEnabled) { start = (ulong)_futex.Cells; bytes = _futex.CellBytes; } break;
            case 6: if (_futexEnabled) { start = (ulong)_futex.Addresses; bytes = _futex.AddressBytes; } break;
            case 7: if (_futexEnabled) { start = (ulong)_futex.Registrations; bytes = _futex.RegistrationBytes; } break;
            default:
                uint offset = index - 8;
                if (offset < kernelCount)
                {
                    if (_owner.Threads->InspectRetainedStorage(_owner.ThreadKey, offset, out start, out bytes, out uint count) != KernelThreadTable.Status.Ok || count != kernelCount) return false;
                }
                else
                {
                    offset -= kernelCount; var w = _owner.Workers[offset / 2].Initial;
                    if ((offset & 1) == 0) { start = (ulong)w.Context; bytes = w.ContextBytes; }
                    else { start = w.StackGuardStart; bytes = w.StackBytes; }
                }
                break;
        }
        return bytes == 0 || Range(start, bytes);
    }
    private bool DescriptorTreesOutsideProcess(LinuxDescriptorServices.State* d)
    {
        if (!ProcessRetainedShape(d, out uint kernelCount)) return false;
        for (uint i = 0; i < 8 + kernelCount + 2 * _owner.Capacity; i++)
            if (!ProcessExtentAt(i, kernelCount, out ulong start, out ulong bytes) ||
                bytes != 0 && !DescriptorTreesOutsideRange(d, start, bytes)) return false;
        return true;
    }
    /// <summary>Check finite retained process/descriptor backing on its original
    /// serialized supervisor. Dirty/orphan/stopped backing remains excluded;
    /// no I/O/copy/CPU/mapping authority follows. The original attachment and
    /// every range stay mapped through return. Structural socket inspectors
    /// must not invoke this active entry gate.</summary>
    internal static bool DescriptorProcessCurrent(LinuxDescriptorServices.State* d)
    {
        if (!Range((ulong)d, (ulong)sizeof(LinuxDescriptorServices.State))) return false;
        if (!FiniteDescriptorTree(d) || d->ThreadOwner == 0) return true;
        if (!Range(d->ThreadOwner, (ulong)sizeof(LinuxThreadServices)) || (d->ThreadOwner & 7) != 0) return false;
        return ((LinuxThreadServices*)d->ThreadOwner)->DescriptorTreesOutsideProcess(d);
    }
    /// <summary>Exclude an actual external packet range from the original
    /// retained process tree. The serialized owner holds all mapped backing;
    /// no callback, global Audit, allocation, I/O, synthetic flags or current
    /// user-copy authority follow. Failed/stopping process backing remains
    /// excluded while its independent network owner drains final traffic.</summary>
    internal static bool DescriptorProcessOutsideStorage(LinuxDescriptorServices.State* d, ulong address, ulong bytes)
    {
        if (!Range((ulong)d, (ulong)sizeof(LinuxDescriptorServices.State)) || !Range(address, bytes)) return false;
        if (!FiniteDescriptorTree(d) || d->ThreadOwner == 0) return true;
        if (!Range(d->ThreadOwner, (ulong)sizeof(LinuxThreadServices)) || (d->ThreadOwner & 7) != 0) return false;
        var p = (LinuxThreadServices*)d->ThreadOwner;
        if (!p->ProcessRetainedShape(d, out uint kernelCount)) return false;
        for (uint i = 0; i < 8 + kernelCount + 2 * p->_owner.Capacity; i++)
            if (!p->ProcessExtentAt(i, kernelCount, out ulong start, out ulong count) ||
                count != 0 && Overlap(address, bytes, start, count)) return false;
        return true;
    }
    /// <summary>Prove an unpublished transport/carrier range before ownership
    /// transfer, with the same structural exclusions as packet storage. Failed
    /// or stopping processes may drain owned traffic but cannot publish a new
    /// transport lifetime. Rejection leaves the candidate untouched.</summary>
    internal static bool CanPublishDescriptorStorage(LinuxDescriptorServices.State* d, ulong address, ulong bytes)
    {
        if (!DescriptorProcessOutsideStorage(d, address, bytes)) return false;
        if (!FiniteDescriptorTree(d) || d->ThreadOwner == 0) return true;
        var p = (LinuxThreadServices*)d->ThreadOwner;
        return !p->_failed && !p->_stopping;
    }
#endif
    private const uint DescriptorExtentCount = 25
#if !NOSTDLIB_EFI || M16_025_PROBE
        + LinuxDescriptorServices.DiskExtentCount
#endif
        ;
    private static void DescriptorExtent(LinuxDescriptorServices.State* state, uint index, out ulong start, out ulong bytes)
    {
        var a = state->Owner; start = bytes = 0;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (index >= 25) { LinuxDescriptorServices.DiskExtentAt(state, index - 25, out start, out bytes); return; }
#endif
        switch (index)
        {
            case 0: start = (ulong)state; bytes = (uint)sizeof(LinuxDescriptorServices.State); break;
            case 1: start = (ulong)a.Slots; bytes = a.DescriptorCount * (uint)sizeof(LinuxDescriptorServices.Slot); break;
            case 2: start = (ulong)a.Descriptions; bytes = a.DescriptionCount * (uint)sizeof(LinuxDescriptorServices.Description); break;
            case 3: start = (ulong)a.Pipes; bytes = a.PipeCount * (uint)sizeof(LinuxPipe); break;
            case 4: start = (ulong)a.PipeBuffers; bytes = a.PipeCount * a.PipeCapacity; break;
            case 5: start = (ulong)a.Snapshots; bytes = a.DescriptionCount * LinuxDescriptorServices.SnapshotBytes; break;
            case 6: start = (ulong)a.Scratch; bytes = a.ScratchBytes; break;
            case 7: start = (ulong)a.Memory; bytes = (uint)sizeof(LinuxMemoryServices.State); break;
            case 8: start = (ulong)a.Memory->Memory.Pool; bytes = a.Memory->Memory.PoolPages * 4096UL; break;
            case 9: start = (ulong)a.Memory->Memory.Tables; bytes = a.Memory->Memory.TablePages * 4096UL; break;
            case 10: start = (ulong)a.Memory->Memory.CandidateTables; bytes = a.Memory->Memory.TablePages * 4096UL; break;
            case 11: start = (ulong)a.Memory->Memory.StaticRanges; bytes = a.Memory->Memory.StaticCount * (uint)sizeof(X64UserMappingRange); break;
            case 12: start = (ulong)a.Views; bytes = a.ViewCount * (uint)sizeof(LinuxDescriptorServices.View); break;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
            case 13: start = (ulong)state->Sockets; bytes = state->Sockets == null ? 0 : (uint)sizeof(LinuxSocketServices.State); break;
            case 14:
                if (state->Sockets != null) { start = (ulong)state->Sockets->Slots; bytes = state->Sockets->Count * (uint)sizeof(LinuxSocketServices.Endpoint); }
                break;
            case 15: start = (ulong)state->Epoll; bytes = state->Epoll == null ? 0 : (uint)sizeof(LinuxEpollServices.State); break;
            case 16:
                if (state->Epoll != null) { start = (ulong)state->Epoll->Ports; bytes = state->Epoll->PortCount * (uint)sizeof(LinuxEpollServices.Port); }
                break;
            case 17:
                if (state->Epoll != null) { start = (ulong)state->Epoll->Registrations; bytes = state->Epoll->RegistrationCount * (uint)sizeof(LinuxEpollServices.Registration); }
                break;
            case >= 18 and < 25:
                if (state->Sockets != null) LinuxSocketNetwork.ExtentAt(state->Sockets->Network, index - 18, out start, out bytes);
                break;
#endif
        }
    }
}
