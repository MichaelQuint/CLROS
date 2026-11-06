namespace OS.Kernel.Diagnostics;

public enum SerialDiagnosticsGuardStatus
{
    FirmwareConsoleOnly = 0,
    OwnedSerialReady = 1,
    FirmwareServicesUnavailable = 2,
    SerialUnavailable = 3,
    InvalidTransition = 4
}
