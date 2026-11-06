using OS.Kernel.Platform;

namespace OS.Boot;
/// <summary>
/// At most two final-map/exit windows, using separate retained one-shot states
/// and the same already-owned writable buffer. Only a returned exit call with
/// EFI_INVALID_PARAMETER permits another capture. Capture/metadata/admission
/// failures and every other exit status stop immediately. No allocation,
/// buffer growth, logging, unrelated firmware calls or mode transition occurs.
/// The caller must retain one zero-initialized state, never copy/reset it,
/// serialize access, and supply authentic mapped storage and entry points.
/// Callback exceptions and hostile mutation are not contained. After success,
/// the caller must invalidate firmware access and perform the owned handoff.
/// </summary>

public static unsafe class BoundedRetryStaleKeyStatus
{
    public const uint MaximumAttempts = 2;
    public static BoundedExitRetryResult Execute(ref BoundedExitRetryState state, FirmwareServicesMode mode, delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap, delegate* unmanaged<void*, ulong, ulong> exitBootServices, void* imageHandle, byte* ownedBuffer, ulong capacity, uint maximumAttempts = MaximumAttempts)
    {
        if (state.Phase == 1)
            return new(BoundedExitRetryStatus.ReentryRejected);
        if (state.Phase != 0)
            return new(BoundedExitRetryStatus.AlreadyConsumed);
        if (maximumAttempts == 0 || maximumAttempts > MaximumAttempts)
            return new(BoundedExitRetryStatus.InvalidAttemptLimit);
        ulong start = (ulong)ownedBuffer;
        if (start == 0 || capacity < UefiMemoryMapCopyPlan.MinimumDescriptorSize || capacity > UefiMemoryMapCopyPlan.MaximumUnsignedLong - start)
            return new(BoundedExitRetryStatus.InvalidOwnedBuffer);
        fixed (BoundedExitRetryState* address = &state)
        {
            ulong stateStart = (ulong)address;
            if (stateStart > UefiMemoryMapCopyPlan.MaximumUnsignedLong - (ulong)sizeof(BoundedExitRetryState) || (start < stateStart + (ulong)sizeof(BoundedExitRetryState) && stateStart < start + capacity))
                return new(BoundedExitRetryStatus.InvalidOwnedBuffer);
        }

        state.Phase = 1;
        uint windows = 0, calls = 0;
        BoundedExitRetryStatus status;
        FinalMapExitWindowResult last;
        while (true)
        {
            ref FinalMapExitWindowState window = ref state.First;
            if (windows != 0)
                window = ref state.Retry;
            last = NoAllocationOrUnrelatedFirmwareCallOccursBetween.Execute(ref window, mode, getMemoryMap, exitBootServices, imageHandle, ownedBuffer, capacity);
            windows++;
            if (last.Exit.CallIssued)
                calls++;
            if (last.Succeeded)
            {
                status = BoundedExitRetryStatus.Completed;
                break;
            }

            if (last.Status != FinalMapExitWindowStatus.Returned)
            {
                status = BoundedExitRetryStatus.WindowFailed;
                break;
            }

            if (!last.Exit.MapKeyRejected)
            {
                status = BoundedExitRetryStatus.ExitFailed;
                break;
            }

            if (windows == maximumAttempts)
            {
                status = BoundedExitRetryStatus.RetryExhausted;
                break;
            }
        }

        state.Phase = 2;
        return new(status, windows, calls, last);
    }
}
