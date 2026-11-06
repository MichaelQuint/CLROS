using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public static class DiagnosticsChainReadiness
{
    public static DiagnosticsChainReadinessResult RequireReady(Com1PortIoBackendPlanResult serialBackend, DescriptorTableInstallReadinessResult descriptorTables, FaultProbeReadinessResult faultProbe, CpuFaultPanicReadinessResult cpuFaultPanic, ExecutableStubBundleMaterializerResult stubMaterialization)
    {
        if (!serialBackend.Succeeded)
        {
            return DiagnosticsChainReadinessResult.Fail(DiagnosticsChainReadinessStatus.SerialBackendNotReady, serialBackend, descriptorTables, faultProbe, cpuFaultPanic, stubMaterialization);
        }

        if (!descriptorTables.Succeeded || descriptorTables.Status != DescriptorTableInstallReadinessStatus.Ready)
        {
            return DiagnosticsChainReadinessResult.Fail(DiagnosticsChainReadinessStatus.DescriptorTablesNotReady, serialBackend, descriptorTables, faultProbe, cpuFaultPanic, stubMaterialization);
        }

        if (!faultProbe.Succeeded || faultProbe.Status != FaultProbeReadinessStatus.Ready)
        {
            return DiagnosticsChainReadinessResult.Fail(DiagnosticsChainReadinessStatus.FaultProbeNotReady, serialBackend, descriptorTables, faultProbe, cpuFaultPanic, stubMaterialization);
        }

        if (!cpuFaultPanic.Succeeded || cpuFaultPanic.Status != CpuFaultPanicReadinessStatus.Ready)
        {
            return DiagnosticsChainReadinessResult.Fail(DiagnosticsChainReadinessStatus.CpuFaultPanicNotReady, serialBackend, descriptorTables, faultProbe, cpuFaultPanic, stubMaterialization);
        }

        if (!stubMaterialization.Succeeded || stubMaterialization.BytesCopied == 0)
        {
            return DiagnosticsChainReadinessResult.Fail(DiagnosticsChainReadinessStatus.StubMaterializationNotReady, serialBackend, descriptorTables, faultProbe, cpuFaultPanic, stubMaterialization);
        }

        return DiagnosticsChainReadinessResult.Pass(serialBackend, descriptorTables, faultProbe, cpuFaultPanic, stubMaterialization);
    }
}
