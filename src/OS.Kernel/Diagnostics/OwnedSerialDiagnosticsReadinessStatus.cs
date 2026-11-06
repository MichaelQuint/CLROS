using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum OwnedSerialDiagnosticsReadinessStatus
{
    Success = 0,
    BackendPlanFailed = 1,
    BackendContractFailed = 2
}
