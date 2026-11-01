using OS.Kernel.Platform;

namespace OS.Boot;
public readonly struct TypedExitBootServicesResult
{
    internal TypedExitBootServicesResult(TypedExitBootServicesStatus status, FirmwareServicesMode mode, ulong firmwareStatus = 0)
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
