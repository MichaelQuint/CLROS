using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum Com1PortIoBackendPlanStatus
{
    Success = 0,
    InvalidSerialConfiguration = 1,
    MissingOperationPlan = 2,
    MissingStubPlan = 3,
    StubMemoryPlanUnavailable = 4,
    StubMemoryPlanTooSmall = 5
}
