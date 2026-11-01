using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public readonly struct DiagnosticsChainReadinessResult
{
    private DiagnosticsChainReadinessResult(bool isInitialized, DiagnosticsChainReadinessStatus status, Com1PortIoBackendPlanStatus serialBackendStatus, DescriptorTableInstallReadinessStatus descriptorTableStatus, FaultProbeReadinessStatus faultProbeStatus, CpuFaultPanicReadinessStatus cpuFaultPanicStatus, ExecutableStubBundleMaterializerStatus stubMaterializationStatus, nuint stubMaterializationBytesCopied)
    {
        IsInitialized = isInitialized;
        Status = status;
        SerialBackendStatus = serialBackendStatus;
        DescriptorTableStatus = descriptorTableStatus;
        FaultProbeStatus = faultProbeStatus;
        CpuFaultPanicStatus = cpuFaultPanicStatus;
        StubMaterializationStatus = stubMaterializationStatus;
        StubMaterializationBytesCopied = stubMaterializationBytesCopied;
    }

    public bool IsInitialized { get; }
    public DiagnosticsChainReadinessStatus Status { get; }
    public Com1PortIoBackendPlanStatus SerialBackendStatus { get; }
    public DescriptorTableInstallReadinessStatus DescriptorTableStatus { get; }
    public FaultProbeReadinessStatus FaultProbeStatus { get; }
    public CpuFaultPanicReadinessStatus CpuFaultPanicStatus { get; }
    public ExecutableStubBundleMaterializerStatus StubMaterializationStatus { get; }
    public nuint StubMaterializationBytesCopied { get; }
    public bool Succeeded => IsInitialized && Status == DiagnosticsChainReadinessStatus.Ready && SerialBackendStatus == Com1PortIoBackendPlanStatus.Success && DescriptorTableStatus == DescriptorTableInstallReadinessStatus.Ready && FaultProbeStatus == FaultProbeReadinessStatus.Ready && CpuFaultPanicStatus == CpuFaultPanicReadinessStatus.Ready && StubMaterializationStatus == ExecutableStubBundleMaterializerStatus.Success && StubMaterializationBytesCopied > 0;

    public static DiagnosticsChainReadinessResult Pass(Com1PortIoBackendPlanResult serialBackend, DescriptorTableInstallReadinessResult descriptorTables, FaultProbeReadinessResult faultProbe, CpuFaultPanicReadinessResult cpuFaultPanic, ExecutableStubBundleMaterializerResult stubMaterialization)
    {
        return new DiagnosticsChainReadinessResult(true, DiagnosticsChainReadinessStatus.Ready, serialBackend.Status, descriptorTables.Status, faultProbe.Status, cpuFaultPanic.Status, stubMaterialization.Status, stubMaterialization.BytesCopied);
    }

    public static DiagnosticsChainReadinessResult Fail(DiagnosticsChainReadinessStatus status, Com1PortIoBackendPlanResult serialBackend, DescriptorTableInstallReadinessResult descriptorTables, FaultProbeReadinessResult faultProbe, CpuFaultPanicReadinessResult cpuFaultPanic, ExecutableStubBundleMaterializerResult stubMaterialization)
    {
        return new DiagnosticsChainReadinessResult(true, status, serialBackend.Status, descriptorTables.Status, faultProbe.Status, cpuFaultPanic.Status, stubMaterialization.Status, stubMaterialization.BytesCopied);
    }
}
