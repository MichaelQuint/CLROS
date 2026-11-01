using OS.Kernel.Diagnostics;

namespace OS.Boot;
public enum UefiExitBootServicesLiveWiringStep
{
    CaptureFreshMemoryMap = 0,
    ConfirmOwnedSerialDiagnostics = 1,
    EmitPostFirmwareDiagnosticsReady = 2,
    ValidateExitBootServicesPlan = 3,
    CallFirmwareExitBootServices = 4,
    MarkFirmwareServicesExited = 5,
    RejectFirmwareBackedCalls = 6
}
