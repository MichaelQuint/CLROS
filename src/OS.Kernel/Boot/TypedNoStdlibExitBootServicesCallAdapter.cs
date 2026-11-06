using OS.Kernel.Platform;

namespace OS.Boot;
/// <summary>
/// One typed synchronous call through caller-supplied authority. The caller must
/// retain and serialize one state, supply a valid callable pointer and original
/// opaque image handle, and report current firmware availability truthfully.
/// Non-null is not a proof of pointer provenance or executability. The handle
/// is never dereferenced, and every ulong map key (including zero) is opaque.
/// Rejected admission/calls preserve state. InFlight is published before the
/// callback; any normal return consumes the attempt regardless of EFI status.
/// Callback exceptions, hostile state mutation and concurrent access are not
/// contained. This does not acquire a map, retry, or change global firmware mode.
/// </summary>

public static unsafe class TypedNoStdlibExitBootServicesCallAdapter
{
    public static TypedExitBootServicesStatus Prepare(ref TypedExitBootServicesState state, FirmwareServicesMode mode, delegate* unmanaged<void*, ulong, ulong> function, void* imageHandle)
    {
        if (state.PhaseValue == TypedExitBootServicesPhase.InFlight)
            return TypedExitBootServicesStatus.ReentryRejected;
        if (state.PhaseValue == TypedExitBootServicesPhase.Returned)
            return TypedExitBootServicesStatus.AlreadyAttempted;
        if (state.PhaseValue == TypedExitBootServicesPhase.Ready)
            return TypedExitBootServicesStatus.AlreadyPrepared;
        if (state.PhaseValue != TypedExitBootServicesPhase.Empty || state.Function != null || state.Handle != null || state.Key != 0 || state.ReturnedStatus != 0)
            return TypedExitBootServicesStatus.InvalidState;
        if (mode != FirmwareServicesMode.BootServicesAvailable)
            return TypedExitBootServicesStatus.FirmwareServicesUnavailable;
        if (function == null)
            return TypedExitBootServicesStatus.MissingFunctionPointer;
        if (imageHandle == null)
            return TypedExitBootServicesStatus.MissingImageHandle;
        TypedExitBootServicesState admitted = default;
        admitted.Function = function;
        admitted.Handle = imageHandle;
        admitted.PhaseValue = TypedExitBootServicesPhase.Ready;
        state = admitted;
        return TypedExitBootServicesStatus.Prepared;
    }

    public static TypedExitBootServicesResult Attempt(ref TypedExitBootServicesState state, FirmwareServicesMode mode, ulong mapKey)
    {
        if (state.PhaseValue == TypedExitBootServicesPhase.InFlight)
            return new(TypedExitBootServicesStatus.ReentryRejected, mode);
        if (state.PhaseValue == TypedExitBootServicesPhase.Returned)
            return new(TypedExitBootServicesStatus.AlreadyAttempted, mode);
        if (state.PhaseValue == TypedExitBootServicesPhase.Empty)
            return new(TypedExitBootServicesStatus.NotPrepared, mode);
        if (state.PhaseValue != TypedExitBootServicesPhase.Ready || state.Key != 0 || state.ReturnedStatus != 0)
            return new(TypedExitBootServicesStatus.InvalidState, mode);
        if (mode != FirmwareServicesMode.BootServicesAvailable)
            return new(TypedExitBootServicesStatus.FirmwareServicesUnavailable, mode);
        if (state.Function == null)
            return new(TypedExitBootServicesStatus.MissingFunctionPointer, mode);
        if (state.Handle == null)
            return new(TypedExitBootServicesStatus.MissingImageHandle, mode);
        state.Key = mapKey;
        state.PhaseValue = TypedExitBootServicesPhase.InFlight;
        ulong status = state.Function(state.Handle, mapKey);
        state.ReturnedStatus = status;
        state.PhaseValue = TypedExitBootServicesPhase.Returned;
        return new(TypedExitBootServicesStatus.Returned, mode, status);
    }
}
