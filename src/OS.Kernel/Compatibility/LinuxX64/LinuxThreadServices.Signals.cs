namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
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
        var result = LinuxSignalServices.Deliver(_signals, _self, slot->Kernel, signal, code, _owner.ProcessId,
            &slot->Image, _owner.ObservedMxcsrMask, ref slot->Mask, faultAddress, error, trap);
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
        return new(LinuxSyscallAction.RestoreUser, slot->Image.Value);
    }
}
