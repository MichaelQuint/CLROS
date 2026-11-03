namespace OS.Kernel.Diagnostics;

public readonly struct DescriptorTableInstallReadinessResult
{
    private DescriptorTableInstallReadinessResult(bool succeeded, DescriptorTableInstallReadinessStatus status, IdtTablePlanStatus idtTableStatus, IdtrDescriptorStatus idtrStatus, GdtDescriptorPlanStatus gdtStatus, TssIstPlanStatus tssIstStatus, DescriptorTableInstallStubPlanStatus stubPlanStatus, DescriptorTableInstructionKind operationKind, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Operation = DescriptorTableInstallReadiness.OperationName;
        IdtTableStatus = idtTableStatus;
        IdtrStatus = idtrStatus;
        GdtStatus = gdtStatus;
        TssIstStatus = tssIstStatus;
        StubPlanStatus = stubPlanStatus;
        OperationKind = operationKind;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded
    {
        get;
    }
    public DescriptorTableInstallReadinessStatus Status
    {
        get;
    }
    public string Operation
    {
        get;
    }
    public IdtTablePlanStatus IdtTableStatus
    {
        get;
    }
    public IdtrDescriptorStatus IdtrStatus
    {
        get;
    }
    public GdtDescriptorPlanStatus GdtStatus
    {
        get;
    }
    public TssIstPlanStatus TssIstStatus
    {
        get;
    }
    public DescriptorTableInstallStubPlanStatus StubPlanStatus
    {
        get;
    }
    public DescriptorTableInstructionKind OperationKind
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }

    public static DescriptorTableInstallReadinessResult Pass(IdtTablePlanStatus idtTableStatus, IdtrDescriptorStatus idtrStatus, GdtDescriptorPlanStatus gdtStatus, TssIstPlanStatus tssIstStatus)
    {
        return new DescriptorTableInstallReadinessResult(true, DescriptorTableInstallReadinessStatus.Ready, idtTableStatus, idtrStatus, gdtStatus, tssIstStatus, DescriptorTableInstallStubPlanStatus.Ok, DescriptorTableInstructionKind.Unknown, 0);
    }

    public static DescriptorTableInstallReadinessResult Fail(DescriptorTableInstallReadinessStatus status, IdtTablePlanStatus idtTableStatus, IdtrDescriptorStatus idtrStatus, GdtDescriptorPlanStatus gdtStatus, TssIstPlanStatus tssIstStatus, DescriptorTableInstallStubPlanStatus stubPlanStatus = DescriptorTableInstallStubPlanStatus.Ok, DescriptorTableInstructionKind operationKind = DescriptorTableInstructionKind.Unknown, int requiredBytes = 0)
    {
        return new DescriptorTableInstallReadinessResult(false, status, idtTableStatus, idtrStatus, gdtStatus, tssIstStatus, stubPlanStatus, operationKind, requiredBytes);
    }
}
