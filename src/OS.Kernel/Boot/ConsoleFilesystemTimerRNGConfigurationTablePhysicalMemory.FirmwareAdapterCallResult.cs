using OS.Kernel.Platform;

namespace OS.Boot;
public readonly struct FirmwareAdapterCallResult
{
    internal FirmwareAdapterCallResult(FirmwareAdapterStatus status, ulong firmwareStatus = 0)
    {
        Status = status;
        FirmwareStatus = firmwareStatus;
    }

    public FirmwareAdapterStatus Status { get; }
    public ulong FirmwareStatus { get; }
    public bool Succeeded => Status == FirmwareAdapterStatus.Success;
}
