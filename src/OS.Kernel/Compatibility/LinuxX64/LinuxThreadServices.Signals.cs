namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Resolve pending delivery and completed wait/restart state at an
    /// IRQ-off user-return boundary, then borrow the full current user image.
    /// Capture the complete GPR/FX image and store the actual syscall result
    /// before calling; do not overwrite the returned image with that old result.
    /// RestoreUser requires installing every user field before resumption.
    /// Terminate means actual process shutdown is staged; image remains null
    /// and native restoration/off-stack exit and reap must still run. Rejected
    /// grants no continuation. This neither captures nor executes user code,
    /// schedules a worker, dispatches a syscall, nor changes gate accounting.</summary>
    public LinuxSyscallResult PrepareUserReturn(ulong key, ulong flags, out X64LinuxUserTransition.UserImage* image)
    {
        image = null;
        var delivery = DeliverPendingSignals(key, flags);
        if (delivery is LinuxSignalServices.Delivery.None or LinuxSignalServices.Delivery.Delivered)
        {
            if (!CurrentImage(key, flags, out image)) return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.RestoreUser, image->Value);
        }
        if (delivery is LinuxSignalServices.Delivery.Terminate or LinuxSignalServices.Delivery.BadFrame &&
            Inspect(key, flags, _owner.Threads->Current, out var view) && view.Exiting)
            return new(LinuxSyscallAction.Terminate, exitCode: view.ExitCode);
        return new(LinuxSyscallAction.Rejected);
    }

    /// <summary>Deliver at most one handler from the actual current generation's
    /// directed pending queue, skipping ignored signals and retaining blocked
    /// ones. None means no unblocked work, not a fabricated handler invocation.
    /// Call only after complete capture or wait completion, before installing
    /// the full image. It resolves already completed wait/restart decisions;
    /// asynchronous capture remains the native owner's responsibility.
    /// Directed tgkill siginfo uses this process PID/uid0 and SI_TKILL(-6).
    /// Job-control or rejected delivery retains the pending entry.</summary>
    public LinuxSignalServices.Delivery DeliverPendingSignals(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _failed || _stopping || _signals == null || !Current(flags, out uint index))
            return LinuxSignalServices.Delivery.Rejected;
        Slot* slot = _owner.Slots + index;
        if (slot->Exiting != 0 || slot->Wait.IsValid || slot->PipeOperation.IsActive) return LinuxSignalServices.Delivery.Rejected;
        for (uint attempt = 0; attempt < 31 + LinuxSignalServices.RealtimeQueueCapacity; attempt++)
        {
            long pending = LinuxSignalServices.Next(_signals, _self, slot->Kernel, slot->Mask, out uint signal);
            if (pending < 0) { _failed = true; return LinuxSignalServices.Delivery.Rejected; }
            if (pending == 0) return FinishUncaughtRestart(key, flags, slot);
            var result = DeliverSignal(key, flags, signal, -6);
            if (result is LinuxSignalServices.Delivery.Rejected or LinuxSignalServices.Delivery.Masked or LinuxSignalServices.Delivery.Unsupported) return result;
            if (!LinuxSignalServices.Consume(_signals, _self, slot->Kernel, signal))
            { _failed = true; return LinuxSignalServices.Delivery.Rejected; }
            if (result != LinuxSignalServices.Delivery.Ignored) return result;
        }
        return FinishUncaughtRestart(key, flags, slot);
    }

    private long Tgkill(ulong key, ulong flags, LinuxSyscallFrame frame)
    {
        if (_signals == null) return Unsupported;
        int group = unchecked((int)frame.Argument0), tid = unchecked((int)frame.Argument1), signal = unchecked((int)frame.Argument2);
        if (group <= 0 || tid <= 0) return Invalid;
        if ((uint)group != _owner.ProcessId) return -3;
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* target = _owner.Slots + i;
            if (target->Tid != (uint)tid) continue;
            if (!Lookup(flags, target->Kernel, out uint actual) || actual != i) { _failed = true; return Io; }
            if (signal is < 0 or > 64) return Invalid;
            if (signal == 0) return 0;
            if (signal == 9) return Shutdown(key, flags, 137) ? 0 : Io;
            if (target->Exiting != 0) return 0; // Linux exiting-task race, no pending lifetime acquired.
            bool ignored = LinuxSignalServices.Ignored(_signals, (uint)signal);
            if (!target->Mask.Contains((uint)signal) && LinuxSignalServices.JobControl(_signals, (uint)signal)) return Unsupported;
            long queued = LinuxSignalServices.Queue(_signals, _self, target->Kernel, (uint)signal, target->Mask);
            if (queued != 0) return queued;
            if (target->Wait.IsValid && !target->Mask.Contains((uint)signal) && !ignored && !InterruptWait(flags, target))
            { _failed = true; return Io; }
            return 0;
        }
        return -3;
    }

    /// <summary>Deliver one selected signal to the actual running generation at
    /// an IRQ-off, completely captured user-return boundary. The caller owns
    /// pending selection and the trusted Linux siginfo code/fault provenance;
    /// it must retain Masked results. This API does not select or wake a worker,
    /// run a handler, or capture asynchronous user state. It rejects pending
    /// waits and supplies no job-control semantics. Delivered mutates only the
    /// owned user image, mask and user stack; install the entire image before
    /// resumption. Bad frames/default fatal actions stage actual owned shutdown.</summary>
    public LinuxSignalServices.Delivery DeliverSignal(ulong key, ulong flags, uint signal, int code,
        ulong faultAddress = 0, ulong error = 0, uint trap = 0)
    {
        if (!Access(key, flags) || _failed || _stopping || _signals == null || !Current(flags, out uint index))
            return LinuxSignalServices.Delivery.Rejected;
        Slot* slot = _owner.Slots + index;
        if (slot->Exiting != 0 || slot->Wait.IsValid || slot->PipeOperation.IsActive) return LinuxSignalServices.Delivery.Rejected;
        ulong priorRip = slot->Image.Frame.Rip; long priorValue = slot->Image.Value;
        bool rewind = signal is >= 1 and <= 64 && !slot->Mask.Contains(signal) && slot->RestartMode == 1 &&
            LinuxSignalServices.RestartHandler(_signals, signal);
        if (rewind && !Rewind(slot, slot->WaitCall.Number))
        { return Shutdown(key, flags, 139) ? LinuxSignalServices.Delivery.BadFrame : LinuxSignalServices.Delivery.Rejected; }
        var result = LinuxSignalServices.Deliver(_signals, _self, slot->Kernel, signal, code, _owner.ProcessId,
            &slot->Image, _owner.ObservedMxcsrMask, ref slot->Mask, faultAddress, error, trap);
        if (result == LinuxSignalServices.Delivery.Delivered && slot->RestartMode is 1 or 2) ClearWaitRestartDecision(slot);
        else if (rewind && result is LinuxSignalServices.Delivery.Rejected or LinuxSignalServices.Delivery.Masked or LinuxSignalServices.Delivery.Unsupported)
        { slot->Image.Frame.Rip = priorRip; slot->Image.Value = priorValue; }
        if (result is LinuxSignalServices.Delivery.BadFrame or LinuxSignalServices.Delivery.Terminate)
        {
            uint reason = result == LinuxSignalServices.Delivery.BadFrame ? 11U : signal;
            if (!Shutdown(key, flags, 128 + reason)) return LinuxSignalServices.Delivery.Rejected;
        }
        return result;
    }

    private LinuxSyscallResult SignalReturn(ulong key, ulong flags, Slot* slot, ref LinuxSyscallFrame frame)
    {
        if (_signals == null) return new(LinuxSyscallAction.Return, Unsupported);
        // The native owner has captured all current GPR/FX bytes before dispatch.
        slot->Image.Frame = frame;
        if (!LinuxSignalServices.Return(_signals, _self, slot->Kernel, &slot->Image, _owner.ObservedMxcsrMask, ref slot->Mask))
        {
            if (!Shutdown(key, flags, 139)) return new(LinuxSyscallAction.Rejected);
            return new(LinuxSyscallAction.Terminate, exitCode: 139);
        }
        frame = slot->Image.Frame;
        ClearRestart(slot); // Linux rt_sigreturn invalidates restart_block.fn even after user edits.
        return new(LinuxSyscallAction.RestoreUser, slot->Image.Value);
    }
}
