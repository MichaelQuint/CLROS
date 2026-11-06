using OS.Boot;
using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum OwnedCom1DiagnosticsReadinessStatus
{
    Success = 0,
    BackendPlanFailed = 1,
    BackendContractFailed = 2,
    OwnedSerialReadinessFailed = 3,
    GuardTransitionFailed = 4,
    PostFirmwareDiagnosticsBlocked = 5,
    PostFirmwareMarkerFailed = 6,
    MissingGuard = 7
}
