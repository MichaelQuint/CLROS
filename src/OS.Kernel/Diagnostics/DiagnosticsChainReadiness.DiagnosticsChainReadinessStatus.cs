using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public enum DiagnosticsChainReadinessStatus
{
    Ready = 0,
    SerialBackendNotReady = 1,
    DescriptorTablesNotReady = 2,
    FaultProbeNotReady = 3,
    CpuFaultPanicNotReady = 4,
    StubMaterializationNotReady = 5
}
