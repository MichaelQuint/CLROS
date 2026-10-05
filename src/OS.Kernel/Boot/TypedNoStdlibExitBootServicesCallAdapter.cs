using OS.Kernel.Platform;

namespace OS.Boot;

public enum TypedExitBootServicesPhase : uint
{
    Empty = 0,
    Ready = 1,
    InFlight = 2,
    Returned = 3
}

public enum TypedExitBootServicesStatus
{
    NotStarted = 0,
    Prepared,
    Returned,
    MissingFunctionPointer,
    MissingImageHandle,
    FirmwareServicesUnavailable,
    NotPrepared,
    AlreadyPrepared,
    ReentryRejected,
    AlreadyAttempted,
    InvalidState
}

// One caller-owned, serialized attempt. Do not copy/reset a live state to retry.
public unsafe struct TypedExitBootServicesState
{
    internal delegate* unmanaged<void*, ulong, ulong> Function;
    internal void* Handle;
    internal ulong Key;
    internal ulong ReturnedStatus;
    internal TypedExitBootServicesPhase PhaseValue;

    public TypedExitBootServicesPhase Phase => PhaseValue;
    public ulong ImageHandle => (ulong)Handle;
    public ulong MapKey => Key;
    public ulong FirmwareStatus => ReturnedStatus;
}

public readonly struct TypedExitBootServicesResult
{
    internal TypedExitBootServicesResult(TypedExitBootServicesStatus status,
        FirmwareServicesMode mode, ulong firmwareStatus = 0)
    {
        Status = status;
        Mode = mode;
        FirmwareStatus = firmwareStatus;
    }

    public TypedExitBootServicesStatus Status { get; }
    public FirmwareServicesMode Mode { get; }
    public ulong FirmwareStatus { get; }
    public bool CallIssued => Status == TypedExitBootServicesStatus.Returned;
    public bool Succeeded => CallIssued && FirmwareStatus == 0;
    public bool MapKeyRejected => CallIssued && FirmwareStatus == 0x8000000000000002;
}

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
    public static TypedExitBootServicesStatus Prepare(ref TypedExitBootServicesState state,
        FirmwareServicesMode mode, delegate* unmanaged<void*, ulong, ulong> function,
        void* imageHandle)
    {
        if (state.PhaseValue == TypedExitBootServicesPhase.InFlight)
            return TypedExitBootServicesStatus.ReentryRejected;
        if (state.PhaseValue == TypedExitBootServicesPhase.Returned)
            return TypedExitBootServicesStatus.AlreadyAttempted;
        if (state.PhaseValue == TypedExitBootServicesPhase.Ready)
            return TypedExitBootServicesStatus.AlreadyPrepared;
        if (state.PhaseValue != TypedExitBootServicesPhase.Empty || state.Function != null
            || state.Handle != null || state.Key != 0 || state.ReturnedStatus != 0)
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

    public static TypedExitBootServicesResult Attempt(ref TypedExitBootServicesState state,
        FirmwareServicesMode mode, ulong mapKey)
    {
        if (state.PhaseValue == TypedExitBootServicesPhase.InFlight)
            return new(TypedExitBootServicesStatus.ReentryRejected, mode);
        if (state.PhaseValue == TypedExitBootServicesPhase.Returned)
            return new(TypedExitBootServicesStatus.AlreadyAttempted, mode);
        if (state.PhaseValue == TypedExitBootServicesPhase.Empty)
            return new(TypedExitBootServicesStatus.NotPrepared, mode);
        if (state.PhaseValue != TypedExitBootServicesPhase.Ready
            || state.Key != 0 || state.ReturnedStatus != 0)
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
