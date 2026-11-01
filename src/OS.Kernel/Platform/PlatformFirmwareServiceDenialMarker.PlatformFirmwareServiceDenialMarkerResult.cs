using OS.Std.Text;

namespace OS.Kernel.Platform;
public readonly struct PlatformFirmwareServiceDenialMarkerResult
{
    private PlatformFirmwareServiceDenialMarkerResult(bool initialized, PlatformFirmwareServiceDenialMarkerStatus status, int bytesWritten, int requiredBytes, FirmwareServicesMode mode, int operationBytes)
    {
        IsInitialized = initialized;
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Mode = mode;
        OperationBytes = operationBytes;
    }

    public bool IsInitialized { get; }
    public PlatformFirmwareServiceDenialMarkerStatus Status { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public FirmwareServicesMode Mode { get; }
    public int OperationBytes { get; }
    public bool Succeeded => IsInitialized && Status == PlatformFirmwareServiceDenialMarkerStatus.Success;

    public static PlatformFirmwareServiceDenialMarkerResult FromStatus(PlatformFirmwareServiceDenialMarkerStatus status, FirmwareServicesMode mode = FirmwareServicesMode.BootServicesAvailable, int operationBytes = 0, int requiredBytes = 0)
    {
        return new PlatformFirmwareServiceDenialMarkerResult(initialized: true, status, bytesWritten: 0, requiredBytes, mode, operationBytes);
    }

    public static PlatformFirmwareServiceDenialMarkerResult Success(int bytesWritten, int requiredBytes, FirmwareServicesMode mode, int operationBytes)
    {
        return new PlatformFirmwareServiceDenialMarkerResult(initialized: true, PlatformFirmwareServiceDenialMarkerStatus.Success, bytesWritten, requiredBytes, mode, operationBytes);
    }
}
