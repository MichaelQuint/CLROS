using OS.Kernel.Platform;

namespace OS.Boot;

public readonly struct FinalMapExitWindowResult
{
    internal FinalMapExitWindowResult(FinalMapExitWindowStatus status, FirmwareMemoryMapRecord map = default, ulong captureStatus = 0, ulong requiredBytes = 0, UefiMemoryMapCopyPlanStatus metadataStatus = default, TypedExitBootServicesStatus admissionStatus = default, TypedExitBootServicesResult exit = default)
    {
        Status = status;
        Map = map;
        CaptureStatus = captureStatus;
        RequiredBytes = requiredBytes;
        MetadataStatus = metadataStatus;
        AdmissionStatus = admissionStatus;
        Exit = exit;
    }

    public FinalMapExitWindowStatus Status
    {
        get;
    }
    public FirmwareMemoryMapRecord Map
    {
        get;
    }
    public ulong CaptureStatus
    {
        get;
    }
    public ulong RequiredBytes
    {
        get;
    }
    public UefiMemoryMapCopyPlanStatus MetadataStatus
    {
        get;
    }
    public TypedExitBootServicesStatus AdmissionStatus
    {
        get;
    }
    public TypedExitBootServicesResult Exit
    {
        get;
    }
    public bool Succeeded => Status == FinalMapExitWindowStatus.Returned && Exit.Succeeded;
}
