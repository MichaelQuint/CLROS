using OS.Kernel.Diagnostics;

namespace OS.Boot;
public enum UefiExitBootServicesLiveWiringStatus
{
    ReadyToCallFirmware = 0,
    FreshMemoryMapMissing = 1,
    PostFirmwareDiagnosticsUnavailable = 2,
    ExitBootServicesPlanBlocked = 3,
    FirmwareCallDisabled = 4
}
