using OS.Kernel.Platform;

namespace OS.Boot;

public enum FinalMapExitWindowStatus
{
    NotStarted,
    Returned,
    ReentryRejected,
    AlreadyConsumed,
    MissingGetMemoryMap,
    InvalidOwnedBuffer,
    ExitAdmissionRejected,
    CaptureFailed,
    InvalidMapMetadata
}

public unsafe struct FinalMapExitWindowState
{
    internal uint Phase;
    internal TypedExitBootServicesState Exit;
    public bool IsConsumed => Phase == 2;
}

public readonly struct FinalMapExitWindowResult
{
    internal FinalMapExitWindowResult(FinalMapExitWindowStatus status,
        FirmwareMemoryMapRecord map = default, ulong captureStatus = 0,
        ulong requiredBytes = 0, UefiMemoryMapCopyPlanStatus metadataStatus = default,
        TypedExitBootServicesStatus admissionStatus = default,
        TypedExitBootServicesResult exit = default)
    {
        Status = status;
        Map = map;
        CaptureStatus = captureStatus;
        RequiredBytes = requiredBytes;
        MetadataStatus = metadataStatus;
        AdmissionStatus = admissionStatus;
        Exit = exit;
    }

    public FinalMapExitWindowStatus Status { get; }
    public FirmwareMemoryMapRecord Map { get; }
    public ulong CaptureStatus { get; }
    public ulong RequiredBytes { get; }
    public UefiMemoryMapCopyPlanStatus MetadataStatus { get; }
    public TypedExitBootServicesStatus AdmissionStatus { get; }
    public TypedExitBootServicesResult Exit { get; }
    public bool Succeeded => Status == FinalMapExitWindowStatus.Returned && Exit.Succeeded;
}

/// <summary>
/// A single serialized final-map/exit window. The caller supplies the original
/// firmware entry points and image handle, an already-owned writable buffer,
/// and one retained state that must not be copied or reset. Buffer allocation,
/// diagnostics and all other firmware work belong before this call or after a
/// failed result. Between successful GetMemoryMap and the exit attempt there
/// are only local metadata checks and the typed adapter. The map key is opaque
/// and comes exclusively from that capture. This does not retry, authenticate
/// arbitrary pointers, contain callback exceptions, or change firmware mode.
/// Buffer contents may change on any firmware return. Only an admitted map is
/// exposed in the result; admission does not validate individual descriptors.
/// </summary>
public static unsafe class NoAllocationOrUnrelatedFirmwareCallOccursBetween
{
    public static FinalMapExitWindowResult Execute(ref FinalMapExitWindowState state,
        FirmwareServicesMode mode,
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        delegate* unmanaged<void*, ulong, ulong> exitBootServices,
        void* imageHandle, byte* ownedBuffer, ulong capacity)
    {
        if (state.Phase == 1)
            return new(FinalMapExitWindowStatus.ReentryRejected);
        if (state.Phase != 0)
            return new(FinalMapExitWindowStatus.AlreadyConsumed);
        if (getMemoryMap == null)
            return new(FinalMapExitWindowStatus.MissingGetMemoryMap);

        ulong start = (ulong)ownedBuffer;
        if (start == 0 || capacity < UefiMemoryMapCopyPlan.MinimumDescriptorSize
            || capacity > UefiMemoryMapCopyPlan.MaximumUnsignedLong - start)
            return new(FinalMapExitWindowStatus.InvalidOwnedBuffer);
        fixed (FinalMapExitWindowState* stateAddress = &state)
        {
            ulong stateStart = (ulong)stateAddress;
            if (stateStart > UefiMemoryMapCopyPlan.MaximumUnsignedLong - (ulong)sizeof(FinalMapExitWindowState)
                || (start < stateStart + (ulong)sizeof(FinalMapExitWindowState)
                    && stateStart < start + capacity))
                return new(FinalMapExitWindowStatus.InvalidOwnedBuffer);
        }

        TypedExitBootServicesStatus admission = TypedNoStdlibExitBootServicesCallAdapter.Prepare(
            ref state.Exit, mode, exitBootServices, imageHandle);
        if (admission != TypedExitBootServicesStatus.Prepared)
            return new(FinalMapExitWindowStatus.ExitAdmissionRejected, admissionStatus: admission);

        state.Phase = 1;
        ulong size = capacity, key = 0, descriptorSize = 0;
        uint descriptorVersion = 0;
        ulong captureStatus = getMemoryMap(&size, ownedBuffer, &key, &descriptorSize, &descriptorVersion);
        if (captureStatus != 0)
        {
            state.Phase = 2;
            return new(FinalMapExitWindowStatus.CaptureFailed,
                captureStatus: captureStatus, requiredBytes: size);
        }

        FirmwareMemoryMapRecord map = default;
        map.BufferAddress = start;
        map.BufferSize = capacity;
        map.SourceBufferAddress = start;
        map.SourceBufferSize = capacity;
        map.MapSize = size;
        map.MapKey = key;
        map.DescriptorSize = descriptorSize;
        map.DescriptorVersion = descriptorVersion;
        map.DescriptorCount = descriptorSize == 0 ? 0 : size / descriptorSize;
        UefiMemoryMapCopyPlanResult validation = UefiMemoryMapCopyPlanResult.Create(map, capacity);
        if (!validation.Succeeded)
        {
            state.Phase = 2;
            return new(FinalMapExitWindowStatus.InvalidMapMetadata,
                requiredBytes: size, metadataStatus: validation.Status);
        }

        TypedExitBootServicesResult exit = TypedNoStdlibExitBootServicesCallAdapter.Attempt(
            ref state.Exit, mode, key);
        state.Phase = 2;
        return new(FinalMapExitWindowStatus.Returned, map, exit: exit);
    }
}
