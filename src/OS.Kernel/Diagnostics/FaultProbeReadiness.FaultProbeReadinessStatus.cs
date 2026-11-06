using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum FaultProbeReadinessStatus
{
    Ready = 0,
    ProbeEnabledByDefault = 1,
    InvalidOpcodeStubFailure = 2,
    NullWriteStubFailure = 3,
    MemoryPlanUnavailable = 4,
    MemoryPlanTooSmall = 5
}
