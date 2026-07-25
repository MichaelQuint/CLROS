namespace OS.Kernel.Diagnostics;

public enum DescriptorTableInstallReadinessStatus
{
    Ready = 0,
    IdtTableNotReady = 1,
    IdtrNotReady = 2,
    GdtNotReady = 3,
    TssIstNotReady = 4,
    DoubleFaultIstNotReady = 5,
    StubPlanNotReady = 6,
    OutputBufferTooSmall = 7
}

public readonly struct DescriptorTableInstallReadinessResult
{
    private DescriptorTableInstallReadinessResult(
        bool succeeded,
        DescriptorTableInstallReadinessStatus status,
        IdtTablePlanStatus idtTableStatus,
        IdtrDescriptorStatus idtrStatus,
        GdtDescriptorPlanStatus gdtStatus,
        TssIstPlanStatus tssIstStatus,
        DescriptorTableInstallStubPlanStatus stubPlanStatus,
        DescriptorTableInstructionKind operationKind,
        int requiredBytes)
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

    public bool Succeeded { get; }

    public DescriptorTableInstallReadinessStatus Status { get; }

    public string Operation { get; }

    public IdtTablePlanStatus IdtTableStatus { get; }

    public IdtrDescriptorStatus IdtrStatus { get; }

    public GdtDescriptorPlanStatus GdtStatus { get; }

    public TssIstPlanStatus TssIstStatus { get; }

    public DescriptorTableInstallStubPlanStatus StubPlanStatus { get; }

    public DescriptorTableInstructionKind OperationKind { get; }

    public int RequiredBytes { get; }

    public static DescriptorTableInstallReadinessResult Pass(
        IdtTablePlanStatus idtTableStatus,
        IdtrDescriptorStatus idtrStatus,
        GdtDescriptorPlanStatus gdtStatus,
        TssIstPlanStatus tssIstStatus)
    {
        return new DescriptorTableInstallReadinessResult(
            true,
            DescriptorTableInstallReadinessStatus.Ready,
            idtTableStatus,
            idtrStatus,
            gdtStatus,
            tssIstStatus,
            DescriptorTableInstallStubPlanStatus.Ok,
            DescriptorTableInstructionKind.Unknown,
            0);
    }

    public static DescriptorTableInstallReadinessResult Fail(
        DescriptorTableInstallReadinessStatus status,
        IdtTablePlanStatus idtTableStatus,
        IdtrDescriptorStatus idtrStatus,
        GdtDescriptorPlanStatus gdtStatus,
        TssIstPlanStatus tssIstStatus,
        DescriptorTableInstallStubPlanStatus stubPlanStatus = DescriptorTableInstallStubPlanStatus.Ok,
        DescriptorTableInstructionKind operationKind = DescriptorTableInstructionKind.Unknown,
        int requiredBytes = 0)
    {
        return new DescriptorTableInstallReadinessResult(
            false,
            status,
            idtTableStatus,
            idtrStatus,
            gdtStatus,
            tssIstStatus,
            stubPlanStatus,
            operationKind,
            requiredBytes);
    }
}

public static class DescriptorTableInstallReadiness
{
    public const string OperationName = "descriptor-table.install.ready";

    public static DescriptorTableInstallReadinessResult RequireReady(
        IdtTablePlanResult idtTableResult,
        IdtrDescriptorResult idtrResult,
        GdtDescriptorPlanResult gdtResult,
        TssIstPlanResult tssIstResult,
        bool doubleFaultIstReady,
        ushort tssSelector,
        int lidtStubBufferLength,
        int lgdtStubBufferLength,
        int ltrStubBufferLength)
    {
        if (!idtTableResult.Succeeded || idtTableResult.Status != IdtTablePlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.IdtTableNotReady,
                idtTableResult.Status,
                idtrResult.Status,
                gdtResult.Status,
                tssIstResult.Status);
        }

        if (!idtrResult.Succeeded || idtrResult.Status != IdtrDescriptorStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.IdtrNotReady,
                idtTableResult.Status,
                idtrResult.Status,
                gdtResult.Status,
                tssIstResult.Status);
        }

        if (!gdtResult.Succeeded || gdtResult.Status != GdtDescriptorPlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.GdtNotReady,
                idtTableResult.Status,
                idtrResult.Status,
                gdtResult.Status,
                tssIstResult.Status);
        }

        if (!tssIstResult.Succeeded || tssIstResult.Status != TssIstPlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.TssIstNotReady,
                idtTableResult.Status,
                idtrResult.Status,
                gdtResult.Status,
                tssIstResult.Status);
        }

        if (!doubleFaultIstReady || !TssIstPlan.IsValidIstIndex(tssIstResult.IstIndex))
        {
            return DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.DoubleFaultIstNotReady,
                idtTableResult.Status,
                idtrResult.Status,
                gdtResult.Status,
                tssIstResult.Status);
        }

        DescriptorTableInstallReadinessResult stubReadiness = ValidateStubPlan(
            DescriptorTableInstructionKind.Lidt,
            tssSelector,
            lidtStubBufferLength,
            idtTableResult.Status,
            idtrResult.Status,
            gdtResult.Status,
            tssIstResult.Status);
        if (!stubReadiness.Succeeded)
        {
            return stubReadiness;
        }

        stubReadiness = ValidateStubPlan(
            DescriptorTableInstructionKind.Lgdt,
            tssSelector,
            lgdtStubBufferLength,
            idtTableResult.Status,
            idtrResult.Status,
            gdtResult.Status,
            tssIstResult.Status);
        if (!stubReadiness.Succeeded)
        {
            return stubReadiness;
        }

        stubReadiness = ValidateStubPlan(
            DescriptorTableInstructionKind.Ltr,
            tssSelector,
            ltrStubBufferLength,
            idtTableResult.Status,
            idtrResult.Status,
            gdtResult.Status,
            tssIstResult.Status);
        if (!stubReadiness.Succeeded)
        {
            return stubReadiness;
        }

        return DescriptorTableInstallReadinessResult.Pass(
            idtTableResult.Status,
            idtrResult.Status,
            gdtResult.Status,
            tssIstResult.Status);
    }

    private static DescriptorTableInstallReadinessResult ValidateStubPlan(
        DescriptorTableInstructionKind operationKind,
        ushort tssSelector,
        int outputBufferLength,
        IdtTablePlanStatus idtTableStatus,
        IdtrDescriptorStatus idtrStatus,
        GdtDescriptorPlanStatus gdtStatus,
        TssIstPlanStatus tssIstStatus)
    {
        DescriptorTableInstallStubPlanStatus status = DescriptorTableInstallStubPlan.Validate(
            operationKind,
            DescriptorTableInstallStubPlan.DescriptorPayloadLength,
            tssSelector,
            outputBufferLength);

        if (status == DescriptorTableInstallStubPlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Pass(
                idtTableStatus,
                idtrStatus,
                gdtStatus,
                tssIstStatus);
        }

        DescriptorTableInstallReadinessStatus readinessStatus =
            status == DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall
                ? DescriptorTableInstallReadinessStatus.OutputBufferTooSmall
                : DescriptorTableInstallReadinessStatus.StubPlanNotReady;

        return DescriptorTableInstallReadinessResult.Fail(
            readinessStatus,
            idtTableStatus,
            idtrStatus,
            gdtStatus,
            tssIstStatus,
            status,
            operationKind,
            DescriptorTableInstallStubPlan.GetRequiredByteLength(operationKind));
    }
}
