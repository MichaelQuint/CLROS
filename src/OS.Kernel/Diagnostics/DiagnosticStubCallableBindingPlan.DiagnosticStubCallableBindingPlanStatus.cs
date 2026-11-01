using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public enum DiagnosticStubCallableBindingPlanStatus
{
    Ready = 0,
    InvocationReadinessNotReady = 1,
    BackendContractNotReady = 2,
    BackendContractMismatch = 3,
    MaterializerNotReady = 4,
    Out8BytesNotMaterialized = 5,
    In8BytesNotMaterialized = 6,
    Out8CallableOutsidePayload = 7,
    In8CallableOutsidePayload = 8,
    CallableRangesOverlap = 9,
    InvalidCom1BasePort = 10
}
