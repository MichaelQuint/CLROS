using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class FaultInfrastructureReadinessTests
{
    private const ulong IdtBaseAddress = 0xffff800000100000UL;

    [HostTest("fault infra readiness accepts complete plan", HostLane.Fast, 0)]
    private static bool TestAcceptsCompletePlan()
    {
        FaultInfrastructureReadinessResult result =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: true,
                IdtTablePlanResult.Success(),
                IdtrDescriptor.Create(IdtBaseAddress, IdtTablePlan.EncodedByteLength),
                gdtReady: true,
                tssIstReady: true,
                doubleFaultIstReady: true);

        return HostCheck.Result(result.Succeeded
            && result.Status == FaultInfrastructureReadinessStatus.Ready
            && result.Operation == FaultInfrastructureReadiness.OperationName
            && result.SerialDiagnosticsReady
            && result.IdtTableReady
            && result.IdtTableStatus == IdtTablePlanStatus.Ok
            && result.IdtrReady
            && result.IdtrStatus == IdtrDescriptorStatus.Ok
            && result.GdtReady
            && result.TssIstReady
            && result.DoubleFaultIstReady);
    }

    [HostTest("fault infra readiness rejects missing serial", HostLane.Fast, 1)]
    private static bool TestRejectsMissingSerial()
    {
        FaultInfrastructureReadinessResult result =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: false,
                idtTableReady: false,
                IdtTablePlanStatus.MissingRequiredVector,
                idtrReady: false,
                IdtrDescriptorStatus.NullBaseAddress,
                gdtReady: false,
                tssIstReady: false,
                doubleFaultIstReady: false);

        return HostCheck.Result(!result.Succeeded
            && result.Status == FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady
            && result.Operation == FaultInfrastructureReadiness.OperationName
            && !result.SerialDiagnosticsReady
            && !result.IdtTableReady
            && result.IdtTableStatus == IdtTablePlanStatus.MissingRequiredVector
            && !result.IdtrReady
            && result.IdtrStatus == IdtrDescriptorStatus.NullBaseAddress);
    }

    [HostTest("fault infra readiness rejects incomplete descriptor plans", HostLane.Fast, 2)]
    private static bool TestRejectsIncompleteDescriptorPlans()
    {
        FaultInfrastructureReadinessResult missingIdt =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: true,
                idtTableReady: false,
                IdtTablePlanStatus.MissingRequiredVector,
                idtrReady: false,
                IdtrDescriptorStatus.NullBaseAddress,
                gdtReady: false,
                tssIstReady: false,
                doubleFaultIstReady: false);

        FaultInfrastructureReadinessResult missingIdtr =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: true,
                IdtTablePlanResult.Success(),
                IdtrDescriptorResult.Fail(IdtrDescriptorStatus.NullBaseAddress, 0),
                gdtReady: false,
                tssIstReady: false,
                doubleFaultIstReady: false);

        FaultInfrastructureReadinessResult missingGdt =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: true,
                IdtTablePlanResult.Success(),
                IdtrDescriptor.Create(IdtBaseAddress, IdtTablePlan.EncodedByteLength),
                gdtReady: false,
                tssIstReady: true,
                doubleFaultIstReady: true);

        FaultInfrastructureReadinessResult missingTssIst =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: true,
                IdtTablePlanResult.Success(),
                IdtrDescriptor.Create(IdtBaseAddress, IdtTablePlan.EncodedByteLength),
                gdtReady: true,
                tssIstReady: false,
                doubleFaultIstReady: true);

        FaultInfrastructureReadinessResult missingDoubleFaultIst =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: true,
                IdtTablePlanResult.Success(),
                IdtrDescriptor.Create(IdtBaseAddress, IdtTablePlan.EncodedByteLength),
                gdtReady: true,
                tssIstReady: true,
                doubleFaultIstReady: false);

        return HostCheck.Result(!missingIdt.Succeeded
            && missingIdt.Status == FaultInfrastructureReadinessStatus.IdtTableNotReady
            && missingIdt.IdtTableStatus == IdtTablePlanStatus.MissingRequiredVector
            && !missingIdtr.Succeeded
            && missingIdtr.Status == FaultInfrastructureReadinessStatus.IdtrNotReady
            && missingIdtr.IdtrStatus == IdtrDescriptorStatus.NullBaseAddress
            && !missingGdt.Succeeded
            && missingGdt.Status == FaultInfrastructureReadinessStatus.GdtNotReady
            && !missingTssIst.Succeeded
            && missingTssIst.Status == FaultInfrastructureReadinessStatus.TssIstNotReady
            && !missingDoubleFaultIst.Succeeded
            && missingDoubleFaultIst.Status == FaultInfrastructureReadinessStatus.DoubleFaultIstNotReady);
    }
}
