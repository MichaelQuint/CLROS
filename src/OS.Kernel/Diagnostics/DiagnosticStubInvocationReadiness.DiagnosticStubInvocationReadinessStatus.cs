using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public enum DiagnosticStubInvocationReadinessStatus
{
    Ready = 0,
    PageTransitionNotReady = 1,
    BackendContractNotReady = 2,
    Out8AddressOutsideRegion = 3,
    In8AddressOutsideRegion = 4
}
