#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    private const uint WritableDiskStatBytes = 144;
    /// <summary>Advance one serialized writable request, including an orphan,
    /// then inspect at most16 original worker generations. Only the original
    /// source5 wait with no prior winner is woken. No user copy occurs here.
    /// Dirty recovery retains backing and refuses successful descriptor release.</summary>
    public bool PollWritableDiskReadiness(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _descriptors == null || !LinuxDescriptorServices.Attached(_descriptors, _self))
            return false;
        if (_descriptors->WritableDisk == null)
            return true;
        if (!WritableDiskStorageOutsideProcess(_descriptors, flags))
        {
            _failed = true;
            return false;
        }
        var progress = LinuxDescriptorServices.PumpWritableDiskOne(_descriptors, _self);
        if (progress.Progress == LinuxDescriptorServices.WritableDiskProgress.Rejected)
        {
            _failed = true;
            return false;
        }
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* slot = _owner.Slots + i;
            if (!slot->WritableDiskOperation.IsActive || !slot->Wait.IsValid || slot->WaitSource != 5 || slot->Exiting != 0)
                continue;
            var current = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
            if (!current.Succeeded || current.Wait.Sequence != slot->Wait.Sequence)
            {
                _failed = true;
                return false;
            }
            if (current.Completion != KernelThreadTable.Completion.None)
                continue;
            var observed = LinuxDescriptorServices.PollWritableDisk(_descriptors, _self, slot->WritableDiskOperation);
            if (observed.Progress == LinuxDescriptorServices.WritableDiskProgress.Rejected || observed.Worker.Value != slot->Kernel.Value)
            {
                _failed = true;
                return false;
            }
            if (observed.Progress is not (LinuxDescriptorServices.WritableDiskProgress.Ready or LinuxDescriptorServices.WritableDiskProgress.RecoveryRequired))
                continue;
            if (!_owner.Threads->Wake(_owner.ThreadKey, flags, slot->Wait).Succeeded)
            {
                _failed = true;
                return false;
            }
        }
        return true;
    }

    private bool TryBeginWritableDisk(ulong flags, Slot* slot, LinuxSyscallFrame frame, ulong callOrdinal, out LinuxSyscallResult result)
    {
        result = new(LinuxSyscallAction.Rejected);
        if (!WritableDiskStorageOutsideProcess(_descriptors, flags))
        {
            _failed = true;
            return true;
        }
        var begun = LinuxDescriptorServices.BeginWritableDisk(_descriptors, _self, slot->Kernel, frame, callOrdinal);
        if (begun.Progress == LinuxDescriptorServices.WritableDiskProgress.NotHandled)
            return false;
        if (begun.Progress == LinuxDescriptorServices.WritableDiskProgress.Immediate)
        {
            result = new(LinuxSyscallAction.Return, begun.Value);
            return true;
        }
        if (begun.Progress != LinuxDescriptorServices.WritableDiskProgress.Pending || !begun.Token.IsActive || begun.Worker.Value != slot->Kernel.Value)
        {
            _failed = true;
            return true;
        }
        slot->WritableDiskOperation = begun.Token;
        slot->WaitCall = frame;
        slot->WaitDeadline = 0;
        slot->WritableDiskOutputBytes = CaptureWritableDiskOutputPrefix(frame);
        result = StageWritableDiskWait(flags, slot);
        return true;
    }

    private LinuxSyscallResult StageWritableDiskWait(ulong flags, Slot* slot)
    {
        var begin = _owner.Threads->BeginWait(_owner.ThreadKey, flags, slot->Kernel,
            KernelThreadTable.WaitKind.Park, _owner.Threads->ObservedTicks);
        if (!begin.Succeeded)
        {
            // A completed mutation may already have won. Consume its real
            // result; otherwise revoke output and leave an independent orphan.
            return CancelWritableDiskWait(flags, slot, abandoning: false);
        }
        slot->Wait = begin.Wait;
        slot->WaitSource = 5;
        if (!InterruptPendingWait(flags, slot))
            return new(LinuxSyscallAction.Rejected);
        return new(LinuxSyscallAction.Blocked);
    }

    /// <summary>Commit source5 Parking only after complete CPL3 restoration
    /// and genuine kernel CALL capture. Early readiness stays on the exact wait
    /// sequence. Native transfer/confirmation and retained mapping proof are outer.</summary>
    public bool CommitWritableDiskWait(ulong key, ulong flags, ThreadContextSwitch.Descriptor captured, out bool handoff)
    {
        handoff = false;
        if (!Access(key, flags) || _descriptors == null || !Lookup(flags, _owner.Threads->Current, out uint index))
            return false;
        Slot* slot = _owner.Slots + index;
        if (!slot->WritableDiskOperation.IsActive || !slot->Wait.IsValid || slot->WaitSource != 5)
            return false;
        var parked = _owner.Threads->Park(_owner.ThreadKey, flags, slot->Wait, captured);
        if (!parked.Succeeded)
            return false;
        handoff = parked.State == KernelThreadTable.State.Waiting;
        return true;
    }

    /// <summary>Consume the current original worker's won source5 wait. The
    /// adapter revalidates fd generation, original call and current output before
    /// copying or committing its cursor. No mutation is automatically replayed.
    /// Interrupted pending work revokes copy authority before returning EINTR;
    /// an already-ready non-read result wins. Dirty recovery remains retained.</summary>
    public LinuxSyscallResult CompleteWritableDiskWait(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _descriptors == null || !Current(flags, out uint index))
            return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
        if (!state.Succeeded || !slot->Wait.IsValid || slot->WaitSource != 5 || !slot->WritableDiskOperation.IsActive ||
            state.Wait.Sequence != slot->Wait.Sequence || state.Completion == KernelThreadTable.Completion.None)
            return new(LinuxSyscallAction.Rejected);
        if (_stopping || slot->Exiting != 0)
        {
            if (!DropWritableDiskOperation(flags, slot))
                return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.ThreadExited, exitCode: slot->ExitCode);
        }
        if (_failed)
            return new(LinuxSyscallAction.Rejected);
        if (state.Completion == KernelThreadTable.Completion.Interrupted)
            return CancelWritableDiskWait(flags, slot, abandoning: false);
        if (state.Completion != KernelThreadTable.Completion.Woken)
        {
            _failed = true;
            return new(LinuxSyscallAction.Rejected);
        }
        slot->Wait = default;
        slot->WaitSource = 0;
        return FinishWritableDiskWait(flags, slot);
    }

    private LinuxSyscallResult FinishWritableDiskWait(ulong flags, Slot* slot)
    {
        var completed = LinuxDescriptorServices.CompleteWritableDisk(_descriptors, _self, slot->Kernel,
            slot->WritableDiskOperation, slot->WaitCall, slot->WritableDiskOutputBytes);
        if (completed.Progress == LinuxDescriptorServices.WritableDiskProgress.Immediate ||
            completed.Progress is LinuxDescriptorServices.WritableDiskProgress.Draining or LinuxDescriptorServices.WritableDiskProgress.RecoveryRequired && !completed.Worker.IsValid)
        {
            long value = completed.Progress == LinuxDescriptorServices.WritableDiskProgress.RecoveryRequired ? Io : completed.Value;
            ClearWritableDiskOperation(slot);
            slot->Image.Value = value;
            return new(LinuxSyscallAction.Return, value);
        }
        if (completed.Progress is LinuxDescriptorServices.WritableDiskProgress.Pending or LinuxDescriptorServices.WritableDiskProgress.Draining)
            return StageWritableDiskWait(flags, slot);
        _failed = true;
        return new(LinuxSyscallAction.Rejected);
    }

    private LinuxSyscallResult CancelWritableDiskWait(ulong flags, Slot* slot, bool abandoning)
    {
        var cancelled = LinuxDescriptorServices.CancelWritableDisk(_descriptors, _self, slot->WritableDiskOperation, abandoning);
        if (!abandoning && cancelled.Progress == LinuxDescriptorServices.WritableDiskProgress.Ready && cancelled.Worker.Value == slot->Kernel.Value)
        {
            // No pump or replay here: an original known result is consumed once.
            slot->Wait = default;
            slot->WaitSource = 0;
            return FinishWritableDiskWait(flags, slot);
        }
        if (cancelled.Progress is not (LinuxDescriptorServices.WritableDiskProgress.Immediate or LinuxDescriptorServices.WritableDiskProgress.Draining or LinuxDescriptorServices.WritableDiskProgress.RecoveryRequired) || cancelled.Worker.IsValid)
        {
            _failed = true;
            return new(LinuxSyscallAction.Rejected);
        }
        long value = cancelled.Progress == LinuxDescriptorServices.WritableDiskProgress.RecoveryRequired ? Io : -4;
        ClearWritableDiskOperation(slot);
        slot->Image.Value = value;
        return new(LinuxSyscallAction.Return, value);
    }

    private bool DropWritableDiskOperation(ulong flags, Slot* slot)
    {
        if (!slot->WritableDiskOperation.IsActive)
            return true;
        if (_descriptors == null || !LinuxDescriptorServices.Attached(_descriptors, _self))
        {
            _failed = true;
            return false;
        }
        return CancelWritableDiskWait(flags, slot, abandoning: true).Action == LinuxSyscallAction.Return;
    }

    private static void ClearWritableDiskOperation(Slot* slot)
    {
        slot->WritableDiskOperation = default;
        slot->WritableDiskOutputBytes = 0;
        if (slot->WaitSource == 5)
        {
            slot->Wait = default;
            slot->WaitSource = 0;
        }
        slot->WaitCall = default;
        slot->WaitDeadline = 0;
        slot->RestartMode = 0;
    }

    private uint CaptureWritableDiskOutputPrefix(LinuxSyscallFrame call)
    {
        uint maximum = call.Number is 4 or 5 or 6 ? WritableDiskStatBytes :
            call.Number is 0 or 17 ? (uint)(call.Argument2 < 8192 ? call.Argument2 : 8192) : 0;
        ulong address = call.Argument1;
        uint admitted = 0;
        while (admitted < maximum)
        {
            if (address < 4096 || address >= LinuxSyscallGateway.UserEnd)
                break;
            uint bytes = 4096U - (uint)(address & 4095);
            if (bytes > maximum - admitted)
                bytes = maximum - admitted;
            if (!LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Write, _owner.User))
                break;
            admitted += bytes;
            address += bytes;
        }
        return admitted;
    }

    private bool MayChangeWritableDiskOutput(ulong address, ulong bytes)
    {
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* slot = _owner.Slots + i;
            if (!slot->WritableDiskOperation.IsActive)
                continue;
            uint maximum = slot->WaitCall.Number is 4 or 5 or 6 ? WritableDiskStatBytes : 8192U;
            if (slot->WritableDiskOutputBytes > maximum || slot->WritableDiskOutputBytes != 0 &&
                (slot->WaitCall.Number is not (0 or 4 or 5 or 6 or 17) || !UserExtent(slot->WaitCall.Argument1, slot->WritableDiskOutputBytes) ||
                 Overlap(address, bytes, slot->WaitCall.Argument1, slot->WritableDiskOutputBytes)))
                return false;
        }
        return true;
    }

    /// <summary>Authenticate the original attached thread owner before publishing
    /// a writable attachment. With no thread attachment, EnablePipes performs
    /// the reciprocal check later. This pure query grants no process creation,
    /// I/O or copy authority and does not strengthen retained cleanup audits.</summary>
    internal static bool WritableDiskProcessMatches(LinuxDescriptorServices.State* descriptors, uint processId)
    {
        if (!Range((ulong)descriptors, (ulong)sizeof(LinuxDescriptorServices.State)) || processId == 0)
            return false;
        if (descriptors->ThreadOwner == 0)
            return true;
        if (!Range(descriptors->ThreadOwner, (ulong)sizeof(LinuxThreadServices)) || (descriptors->ThreadOwner & 7) != 0)
            return false;
        var process = (LinuxThreadServices*)descriptors->ThreadOwner;
        return process->ProcessRetainedShape(descriptors, out _) && process->_owner.ProcessId == processId;
    }

    private bool WritableDiskStorageOutsideProcess(LinuxDescriptorServices.State* descriptors, ulong flags)
        => (!FiniteDescriptorTree(descriptors) || DescriptorTreesOutsideProcess(descriptors)) &&
            (descriptors->WritableDisk == null || descriptors->WritableDisk->Process.Pid == _owner.ProcessId);
}
#endif
