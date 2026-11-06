namespace OS.Kernel.Diagnostics;

public static class FaultInfrastructureReadiness
{
    public const string OperationName = "fault.infrastructure.ready-for-privileged-install";
    public static FaultInfrastructureReadinessResult RequireReadyForPrivilegedInstall(bool serialDiagnosticsReady, IdtTablePlanResult idtTableResult, IdtrDescriptorResult idtrResult, bool gdtReady, bool tssIstReady, bool doubleFaultIstReady)
    {
        return RequireReadyForPrivilegedInstall(serialDiagnosticsReady, idtTableResult.Succeeded && idtTableResult.Status == IdtTablePlanStatus.Ok, idtTableResult.Status, idtrResult.Succeeded && idtrResult.Status == IdtrDescriptorStatus.Ok, idtrResult.Status, gdtReady, tssIstReady, doubleFaultIstReady);
    }

    public static FaultInfrastructureReadinessResult RequireReadyForPrivilegedInstall(bool serialDiagnosticsReady, bool idtTableReady, IdtTablePlanStatus idtTableStatus, bool idtrReady, IdtrDescriptorStatus idtrStatus, bool gdtReady, bool tssIstReady, bool doubleFaultIstReady)
    {
        if (!serialDiagnosticsReady)
        {
            return FaultInfrastructureReadinessResult.Fail(FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
        }

        if (!idtTableReady || idtTableStatus != IdtTablePlanStatus.Ok)
        {
            return FaultInfrastructureReadinessResult.Fail(FaultInfrastructureReadinessStatus.IdtTableNotReady, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
        }

        if (!idtrReady || idtrStatus != IdtrDescriptorStatus.Ok)
        {
            return FaultInfrastructureReadinessResult.Fail(FaultInfrastructureReadinessStatus.IdtrNotReady, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
        }

        if (!gdtReady)
        {
            return FaultInfrastructureReadinessResult.Fail(FaultInfrastructureReadinessStatus.GdtNotReady, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
        }

        if (!tssIstReady)
        {
            return FaultInfrastructureReadinessResult.Fail(FaultInfrastructureReadinessStatus.TssIstNotReady, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
        }

        if (!doubleFaultIstReady)
        {
            return FaultInfrastructureReadinessResult.Fail(FaultInfrastructureReadinessStatus.DoubleFaultIstNotReady, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
        }

        return FaultInfrastructureReadinessResult.Pass(serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
    }
}
