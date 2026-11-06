using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    // FUTEX_REQUEUE uses val2 as a signed count, never a timespec. Both keys
    // share this process's namespace and serialized owner. Admit any required
    // destination binding before the first wake; allocation failure wakes nobody.
    // Moving a registration changes only its address FIFO and backing borrow:
    // the scheduler identity, deadline, bitset and original syscall stay intact.
    private LinuxSyscallResult FutexRequeue(ulong flags, LinuxSyscallFrame frame, uint mode)
    {
        int wakeMaximum = unchecked((int)frame.Argument2), moveMaximum = unchecked((int)frame.Argument3);
        if (wakeMaximum < 0 || moveMaximum < 0 || ((frame.Argument0 | frame.Argument4) & 3) != 0)
            return new(LinuxSyscallAction.Return, Invalid);
        if (!UserExtent(frame.Argument0, 4) || !UserExtent(frame.Argument4, 4))
            return new(LinuxSyscallAction.Return, Fault);
        long error = ResolveFutex(flags, frame.Argument0, mode, false, out uint source);
        if (error != 0)
            return RequeueResult(error);
        error = ResolveFutex(flags, frame.Argument4, mode, source != _futex.Capacity && moveMaximum != 0, out uint destination);
        if (error != 0)
            return RequeueResult(error);
        if (source == _futex.Capacity)
            return new(LinuxSyscallAction.Return, 0);

        FutexCell* from = _futex.Cells + source;
        var wake = _addresses.Wake(_futex.OwnerKey, flags, from->Binding, (uint)wakeMaximum);
        if (!wake.Succeeded)
            _failed = true;
        uint moved = 0;
        if (!_failed && source == destination)
        {
            // Linux permits the same non-PI key. Count actual remaining waits
            // without reordering them or manufacturing a completion/reference.
            for (uint i = 0; i < _owner.Capacity && moved < (uint)moveMaximum; i++)
            {
                Slot* pending = _owner.Slots + i;
                if (pending->WaitSource != 1 || !pending->Wait.IsValid || pending->FutexCellIndex != source)
                    continue;
                var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, pending->Kernel);
                if (state.Succeeded && state.Wait.Sequence == pending->Wait.Sequence &&
                    state.Completion == KernelThreadTable.Completion.None &&
                    state.State is KernelThreadTable.State.Parking or KernelThreadTable.State.Waiting)
                    moved++;
            }
        }
        else if (!_failed && destination != _futex.Capacity)
        {
            FutexCell* to = _futex.Cells + destination;
            while (moved < (uint)moveMaximum)
            {
                var move = _addresses.RequeueOne(_futex.OwnerKey, flags, from->Binding, to->Binding);
                if (!move.Succeeded)
                {
                    _failed = true;
                    break;
                }
                if (move.Count == 0)
                    break;
                if (!Lookup(flags, move.Wait.Thread, out uint index) || from->References == 0 || to->References == uint.MaxValue)
                {
                    _failed = true;
                    break;
                }
                Slot* slot = _owner.Slots + index;
                if (slot->WaitSource != 1 || slot->FutexCellIndex != source || slot->Wait.Sequence != move.Wait.Sequence)
                {
                    _failed = true;
                    break;
                }
                slot->FutexCellIndex = destination;
                from->References--;
                to->References++;
                moved++;
            }
        }
        // A newly admitted but unused target must not consume cell capacity.
        // Woken source waits keep their old borrow until actual consumption.
        if (destination != _futex.Capacity && destination != source && _futex.Cells[destination].References == 0)
            UnbindFutexCell(flags, destination);
        if (from->References == 0)
            UnbindFutexCell(flags, source);
        return RequeueResult((long)wake.Count + moved);
    }

    private readonly LinuxSyscallResult RequeueResult(long value) => _failed ?
        new(LinuxSyscallAction.Rejected) : new(LinuxSyscallAction.Return, value);
}
