namespace OS.Kernel.Diagnostics;
public static class DescriptorTableInstallReadiness
{
    public const string OperationName = "descriptor-table.install.ready";
    public static DescriptorTableInstallReadinessResult RequireReady(IdtTablePlanResult idtTableResult, IdtrDescriptorResult idtrResult, GdtDescriptorPlanResult gdtResult, TssIstPlanResult tssIstResult, bool doubleFaultIstReady, ushort tssSelector, int lidtStubBufferLength, int lgdtStubBufferLength, int ltrStubBufferLength)
    {
        if (!idtTableResult.Succeeded || idtTableResult.Status != IdtTablePlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(DescriptorTableInstallReadinessStatus.IdtTableNotReady, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        }

        if (!idtrResult.Succeeded || idtrResult.Status != IdtrDescriptorStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(DescriptorTableInstallReadinessStatus.IdtrNotReady, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        }

        if (!gdtResult.Succeeded || gdtResult.Status != GdtDescriptorPlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(DescriptorTableInstallReadinessStatus.GdtNotReady, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        }

        if (!tssIstResult.Succeeded || tssIstResult.Status != TssIstPlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Fail(DescriptorTableInstallReadinessStatus.TssIstNotReady, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        }

        if (!doubleFaultIstReady || !TssIstPlan.IsValidIstIndex(tssIstResult.IstIndex))
        {
            return DescriptorTableInstallReadinessResult.Fail(DescriptorTableInstallReadinessStatus.DoubleFaultIstNotReady, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        }

        DescriptorTableInstallReadinessResult stubReadiness = ValidateStubPlan(DescriptorTableInstructionKind.Lidt, tssSelector, lidtStubBufferLength, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        if (!stubReadiness.Succeeded)
        {
            return stubReadiness;
        }

        stubReadiness = ValidateStubPlan(DescriptorTableInstructionKind.Lgdt, tssSelector, lgdtStubBufferLength, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        if (!stubReadiness.Succeeded)
        {
            return stubReadiness;
        }

        stubReadiness = ValidateStubPlan(DescriptorTableInstructionKind.Ltr, tssSelector, ltrStubBufferLength, idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
        if (!stubReadiness.Succeeded)
        {
            return stubReadiness;
        }

        return DescriptorTableInstallReadinessResult.Pass(idtTableResult.Status, idtrResult.Status, gdtResult.Status, tssIstResult.Status);
    }

    private static DescriptorTableInstallReadinessResult ValidateStubPlan(DescriptorTableInstructionKind operationKind, ushort tssSelector, int outputBufferLength, IdtTablePlanStatus idtTableStatus, IdtrDescriptorStatus idtrStatus, GdtDescriptorPlanStatus gdtStatus, TssIstPlanStatus tssIstStatus)
    {
        DescriptorTableInstallStubPlanStatus status = DescriptorTableInstallStubPlan.Validate(operationKind, DescriptorTableInstallStubPlan.DescriptorPayloadLength, tssSelector, outputBufferLength);
        if (status == DescriptorTableInstallStubPlanStatus.Ok)
        {
            return DescriptorTableInstallReadinessResult.Pass(idtTableStatus, idtrStatus, gdtStatus, tssIstStatus);
        }

        DescriptorTableInstallReadinessStatus readinessStatus = status == DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall ? DescriptorTableInstallReadinessStatus.OutputBufferTooSmall : DescriptorTableInstallReadinessStatus.StubPlanNotReady;
        return DescriptorTableInstallReadinessResult.Fail(readinessStatus, idtTableStatus, idtrStatus, gdtStatus, tssIstStatus, status, operationKind, DescriptorTableInstallStubPlan.GetRequiredByteLength(operationKind));
    }
}
