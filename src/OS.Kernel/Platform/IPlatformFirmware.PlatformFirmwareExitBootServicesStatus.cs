namespace OS.Kernel.Platform;
public enum PlatformFirmwareExitBootServicesStatus
{
    Unknown = 0,
    Success = 1,
    MapKeyRejected = 2,
    MissingFunctionPointer = 3,
    FirmwareError = 4,
    GuardDenied = 5
}
