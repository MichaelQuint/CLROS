using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    // Check after publishing each new wait, including a retry after stolen
    // readiness. Keep signals queued for the normal captured user-return path;
    // only the existing completion-before-Park transition runs here. A local
    // search mask skips ignored entries without changing the thread's mask.
    private bool InterruptPendingWait(ulong flags, Slot* slot)
    {
        if (_signals == null) return true;
        var search = slot->Mask;
        for (uint attempt = 0; attempt < 31 + LinuxSignalServices.RealtimeQueueCapacity; attempt++)
        {
            long pending = LinuxSignalServices.Next(_signals, _self, slot->Kernel, search, out uint signal);
            if (pending < 0) break;
            if (pending == 0) return true;
            if (!LinuxSignalServices.Ignored(_signals, signal))
            {
                if (InterruptWait(flags, slot)) return true;
                break;
            }
            search = new(search.Blocked | (1UL << (int)(signal - 1)));
        }
        _failed = true; return false;
    }

    private bool InterruptWait(ulong flags, Slot* slot)
    {
        // Expired real-clock deadlines precede an interrupt, even if the CPU
        // has not idled through its timer callback since registering the wait.
        if ((slot->WaitSource == 1 || slot->WaitSource == 3 && slot->WaitDeadline != 0) && (!Clock(out ulong now) ||
            _owner.Threads->Advance(_owner.ThreadKey, flags, now) != KernelThreadTable.Status.Ok)) return false;
        var state = _owner.Threads->Inspect(_owner.ThreadKey, flags, slot->Kernel);
        if (!state.Succeeded || state.Wait.Sequence != slot->Wait.Sequence || slot->WaitSource is not (1 or 2 or 3)) return false;
        if (state.Completion != KernelThreadTable.Completion.None) return true;
        return _owner.Threads->Interrupt(_owner.ThreadKey, flags, slot->Wait).Succeeded;
    }

    private LinuxSignalServices.Delivery FinishUncaughtRestart(ulong key, ulong flags, Slot* slot)
    {
        uint mode = slot->RestartMode;
        if (mode is not (1 or 2)) return LinuxSignalServices.Delivery.None;
        if (!Rewind(slot, mode == 1 ? slot->WaitCall.Number : 219))
            return Shutdown(key, flags, 139) ? LinuxSignalServices.Delivery.BadFrame : LinuxSignalServices.Delivery.Rejected;
        ClearWaitRestartDecision(slot); // Timed callback/deadline remain armed independently.
        return LinuxSignalServices.Delivery.None;
    }

    private bool Rewind(Slot* slot, ulong number)
    {
        ulong rip = slot->Image.Frame.Rip;
        byte* opcode = stackalloc byte[2];
        if (rip < 4098 || !LinuxSyscallGateway.ValidateRange(rip - 2, 2, LinuxUserPageAccess.Execute, _owner.User) ||
            !Input(rip - 2, opcode, 2) || opcode[0] != 0x0f || opcode[1] != 0x05) return false;
        var frame = slot->Image.Frame; frame.Rip -= 2;
        if (!LinuxSyscallGateway.ValidateReturn(frame, _owner.User)) return false;
        slot->Image.Frame = frame; slot->Image.Value = unchecked((long)number); return true;
    }

    private LinuxSyscallResult RestartFutex(ulong flags, Slot* slot)
    {
        if (!slot->TimedRestartArmed) return new(LinuxSyscallAction.Return, -4);
        LinuxSyscallFrame call = slot->TimedRestartCall; ulong deadline = slot->TimedRestartDeadline;
        ClearTimedRestart(slot); // Linux invalidates fn before calling the restart implementation.
        if (!_futexEnabled || call.Number != 202 || call.Argument3 == 0 || deadline > TimeEnd)
        { _failed = true; return new(LinuxSyscallAction.Rejected); }
        return Futex(flags, slot, call, true, deadline);
    }

    private static void ClearWaitRestartDecision(Slot* slot)
    { slot->RestartMode = 0; slot->WaitCall = default; slot->WaitDeadline = 0; }
    private static void ClearTimedRestart(Slot* slot)
    { slot->TimedRestartArmed = false; slot->TimedRestartCall = default; slot->TimedRestartDeadline = 0; }
    private static void ClearRestart(Slot* slot)
    { ClearWaitRestartDecision(slot); ClearTimedRestart(slot); }
}
