namespace OS.Kernel.Platform;
public readonly struct PlatformFirmwareExitBootServicesResult
{
    public const ulong EfiSuccess = 0;
    public const ulong EfiInvalidParameter = 0x8000000000000002;
    private PlatformFirmwareExitBootServicesResult(PlatformFirmwareExitBootServicesStatus status, FirmwareServicesMode mode, ulong firmwareStatus)
    {
        Status = status;
        Mode = mode;
        FirmwareStatus = firmwareStatus;
    }

    public PlatformFirmwareExitBootServicesStatus Status { get; }
    public FirmwareServicesMode Mode { get; }
    public string Operation => PlatformFirmwareOperations.ExitBootServices;
    public ulong FirmwareStatus { get; }
    public bool Succeeded => Status == PlatformFirmwareExitBootServicesStatus.Success;

    public static PlatformFirmwareExitBootServicesResult FromFirmwareStatus(FirmwareCallResult guardResult, bool exitBootServicesFunctionAvailable, ulong firmwareStatus)
    {
        if (!guardResult.Succeeded)
        {
            return new PlatformFirmwareExitBootServicesResult(PlatformFirmwareExitBootServicesStatus.GuardDenied, guardResult.Mode, firmwareStatus);
        }

        if (!exitBootServicesFunctionAvailable)
        {
            return new PlatformFirmwareExitBootServicesResult(PlatformFirmwareExitBootServicesStatus.MissingFunctionPointer, guardResult.Mode, firmwareStatus);
        }

        if (firmwareStatus == EfiSuccess)
        {
            return new PlatformFirmwareExitBootServicesResult(PlatformFirmwareExitBootServicesStatus.Success, guardResult.Mode, firmwareStatus);
        }

        return new PlatformFirmwareExitBootServicesResult(firmwareStatus == EfiInvalidParameter ? PlatformFirmwareExitBootServicesStatus.MapKeyRejected : PlatformFirmwareExitBootServicesStatus.FirmwareError, guardResult.Mode, firmwareStatus);
    }
}
