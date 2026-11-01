namespace OS.Kernel.Diagnostics;
public readonly struct FaultInfrastructureReadinessResult
{
    private FaultInfrastructureReadinessResult(bool succeeded, FaultInfrastructureReadinessStatus status, bool serialDiagnosticsReady, bool idtTableReady, IdtTablePlanStatus idtTableStatus, bool idtrReady, IdtrDescriptorStatus idtrStatus, bool gdtReady, bool tssIstReady, bool doubleFaultIstReady)
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

    public static FaultInfrastructureReadinessResult Pass(bool serialDiagnosticsReady, bool idtTableReady, IdtTablePlanStatus idtTableStatus, bool idtrReady, IdtrDescriptorStatus idtrStatus, bool gdtReady, bool tssIstReady, bool doubleFaultIstReady)
    {
        return new FaultInfrastructureReadinessResult(true, FaultInfrastructureReadinessStatus.Ready, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
    }

    public static FaultInfrastructureReadinessResult Fail(FaultInfrastructureReadinessStatus status, bool serialDiagnosticsReady, bool idtTableReady, IdtTablePlanStatus idtTableStatus, bool idtrReady, IdtrDescriptorStatus idtrStatus, bool gdtReady, bool tssIstReady, bool doubleFaultIstReady)
    {
        return new FaultInfrastructureReadinessResult(false, status, serialDiagnosticsReady, idtTableReady, idtTableStatus, idtrReady, idtrStatus, gdtReady, tssIstReady, doubleFaultIstReady);
    }
}
