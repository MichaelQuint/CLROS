namespace OS.Kernel.Diagnostics;

public enum FaultInfrastructureReadinessStatus
{
    Ready = 0,
    SerialDiagnosticsNotReady = 1,
    IdtTableNotReady = 2,
    IdtrNotReady = 3,
    GdtNotReady = 4,
    TssIstNotReady = 5,
    DoubleFaultIstNotReady = 6
}

public readonly struct FaultInfrastructureReadinessResult
{
    private FaultInfrastructureReadinessResult(
        bool succeeded,
        FaultInfrastructureReadinessStatus status,
        bool serialDiagnosticsReady,
        bool idtTableReady,
        IdtTablePlanStatus idtTableStatus,
        bool idtrReady,
        IdtrDescriptorStatus idtrStatus,
        bool gdtReady,
        bool tssIstReady,
        bool doubleFaultIstReady)
    {
        Succeeded = succeeded;
        Status = status;
        Operation = FaultInfrastructureReadiness.OperationName;
        SerialDiagnosticsReady = serialDiagnosticsReady;
        IdtTableReady = idtTableReady;
        IdtTableStatus = idtTableStatus;
        IdtrReady = idtrReady;
        IdtrStatus = idtrStatus;
        GdtReady = gdtReady;
        TssIstReady = tssIstReady;
        DoubleFaultIstReady = doubleFaultIstReady;
    }

    public bool Succeeded { get; }

    public FaultInfrastructureReadinessStatus Status { get; }

    public string Operation { get; }

    public bool SerialDiagnosticsReady { get; }

    public bool IdtTableReady { get; }

    public IdtTablePlanStatus IdtTableStatus { get; }

    public bool IdtrReady { get; }

    public IdtrDescriptorStatus IdtrStatus { get; }

    public bool GdtReady { get; }

    public bool TssIstReady { get; }

    public bool DoubleFaultIstReady { get; }

    public static FaultInfrastructureReadinessResult Pass(
        bool serialDiagnosticsReady,
        bool idtTableReady,
        IdtTablePlanStatus idtTableStatus,
        bool idtrReady,
        IdtrDescriptorStatus idtrStatus,
        bool gdtReady,
        bool tssIstReady,
        bool doubleFaultIstReady)
    {
        return new FaultInfrastructureReadinessResult(
            true,
            FaultInfrastructureReadinessStatus.Ready,
            serialDiagnosticsReady,
            idtTableReady,
            idtTableStatus,
            idtrReady,
            idtrStatus,
            gdtReady,
            tssIstReady,
            doubleFaultIstReady);
    }

    public static FaultInfrastructureReadinessResult Fail(
        FaultInfrastructureReadinessStatus status,
        bool serialDiagnosticsReady,
        bool idtTableReady,
        IdtTablePlanStatus idtTableStatus,
        bool idtrReady,
        IdtrDescriptorStatus idtrStatus,
        bool gdtReady,
        bool tssIstReady,
        bool doubleFaultIstReady)
    {
        return new FaultInfrastructureReadinessResult(
            false,
            status,
            serialDiagnosticsReady,
            idtTableReady,
            idtTableStatus,
            idtrReady,
            idtrStatus,
            gdtReady,
            tssIstReady,
            doubleFaultIstReady);
    }
}

public static class FaultInfrastructureReadiness
{
    public const string OperationName = "fault.infrastructure.ready-for-privileged-install";

    public static FaultInfrastructureReadinessResult RequireReadyForPrivilegedInstall(
        bool serialDiagnosticsReady,
        IdtTablePlanResult idtTableResult,
        IdtrDescriptorResult idtrResult,
        bool gdtReady,
        bool tssIstReady,
        bool doubleFaultIstReady)
    {
        return RequireReadyForPrivilegedInstall(
            serialDiagnosticsReady,
            idtTableResult.Succeeded && idtTableResult.Status == IdtTablePlanStatus.Ok,
            idtTableResult.Status,
            idtrResult.Succeeded && idtrResult.Status == IdtrDescriptorStatus.Ok,
            idtrResult.Status,
            gdtReady,
            tssIstReady,
            doubleFaultIstReady);
    }

    public static FaultInfrastructureReadinessResult RequireReadyForPrivilegedInstall(
        bool serialDiagnosticsReady,
        bool idtTableReady,
        IdtTablePlanStatus idtTableStatus,
        bool idtrReady,
        IdtrDescriptorStatus idtrStatus,
        bool gdtReady,
        bool tssIstReady,
        bool doubleFaultIstReady)
    {
        if (!serialDiagnosticsReady)
        {
            return FaultInfrastructureReadinessResult.Fail(
                FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady,
                serialDiagnosticsReady,
                idtTableReady,
                idtTableStatus,
                idtrReady,
                idtrStatus,
                gdtReady,
                tssIstReady,
                doubleFaultIstReady);
        }

        if (!idtTableReady || idtTableStatus != IdtTablePlanStatus.Ok)
        {
            return FaultInfrastructureReadinessResult.Fail(
                FaultInfrastructureReadinessStatus.IdtTableNotReady,
                serialDiagnosticsReady,
                idtTableReady,
                idtTableStatus,
                idtrReady,
                idtrStatus,
                gdtReady,
                tssIstReady,
                doubleFaultIstReady);
        }

        if (!idtrReady || idtrStatus != IdtrDescriptorStatus.Ok)
        {
            return FaultInfrastructureReadinessResult.Fail(
                FaultInfrastructureReadinessStatus.IdtrNotReady,
                serialDiagnosticsReady,
                idtTableReady,
                idtTableStatus,
                idtrReady,
                idtrStatus,
                gdtReady,
                tssIstReady,
                doubleFaultIstReady);
        }

        if (!gdtReady)
        {
            return FaultInfrastructureReadinessResult.Fail(
                FaultInfrastructureReadinessStatus.GdtNotReady,
                serialDiagnosticsReady,
                idtTableReady,
                idtTableStatus,
                idtrReady,
                idtrStatus,
                gdtReady,
                tssIstReady,
                doubleFaultIstReady);
        }

        if (!tssIstReady)
        {
            return FaultInfrastructureReadinessResult.Fail(
                FaultInfrastructureReadinessStatus.TssIstNotReady,
                serialDiagnosticsReady,
                idtTableReady,
                idtTableStatus,
                idtrReady,
                idtrStatus,
                gdtReady,
                tssIstReady,
                doubleFaultIstReady);
        }

        if (!doubleFaultIstReady)
        {
            return FaultInfrastructureReadinessResult.Fail(
                FaultInfrastructureReadinessStatus.DoubleFaultIstNotReady,
                serialDiagnosticsReady,
                idtTableReady,
                idtTableStatus,
                idtrReady,
                idtrStatus,
                gdtReady,
                tssIstReady,
                doubleFaultIstReady);
        }

        return FaultInfrastructureReadinessResult.Pass(
            serialDiagnosticsReady,
            idtTableReady,
            idtTableStatus,
            idtrReady,
            idtrStatus,
            gdtReady,
            tssIstReady,
            doubleFaultIstReady);
    }
}
