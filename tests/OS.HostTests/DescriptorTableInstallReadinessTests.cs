using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class DescriptorTableInstallReadinessTests
{
    private const ulong IdtBaseAddress = 0xffff800000100000UL;
    private const ulong TssBaseAddress = 0xffff800000200000UL;
    private const ulong Rsp0StackTop = 0xffff800000300000UL;
    private const ulong DoubleFaultStackTop = 0xffff800000400000UL;
    private const byte DoubleFaultIstIndex = 1;

    [HostTest("descriptor install readiness accepts complete prerequisites", HostLane.Fast, 0)]
    private static bool TestAcceptsCompletePrerequisites()
    {
        DescriptorTableInstallReadinessResult result = CreateReadyResult();

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstallReadinessStatus.Ready
            && result.Operation == DescriptorTableInstallReadiness.OperationName
            && result.IdtTableStatus == IdtTablePlanStatus.Ok
            && result.IdtrStatus == IdtrDescriptorStatus.Ok
            && result.GdtStatus == GdtDescriptorPlanStatus.Ok
            && result.TssIstStatus == TssIstPlanStatus.Ok
            && result.StubPlanStatus == DescriptorTableInstallStubPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Unknown
            && result.RequiredBytes == 0);
    }

    [HostTest("descriptor install readiness rejects missing idt readiness", HostLane.Fast, 1)]
    private static bool TestRejectsMissingIdtReadiness()
    {
        DescriptorTableInstallReadinessResult result =
            DescriptorTableInstallReadiness.RequireReady(
                IdtTablePlanResult.Fail(IdtTablePlanStatus.MissingRequiredVector, FaultVectorCatalog.PageFault),
                CreateIdtrResult(),
                CreateGdtResult(),
                CreateTssIstResult(),
                doubleFaultIstReady: true,
                GdtDescriptorPlan.TssSelector,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);

        return HostCheck.Result(!result.Succeeded
            && result.Status == DescriptorTableInstallReadinessStatus.IdtTableNotReady
            && result.IdtTableStatus == IdtTablePlanStatus.MissingRequiredVector
            && result.IdtrStatus == IdtrDescriptorStatus.Ok
            && result.GdtStatus == GdtDescriptorPlanStatus.Ok
            && result.TssIstStatus == TssIstPlanStatus.Ok
            && result.StubPlanStatus == DescriptorTableInstallStubPlanStatus.Ok);
    }

    [HostTest("descriptor install readiness rejects missing tss ist readiness", HostLane.Fast, 2)]
    private static bool TestRejectsMissingTssIstReadiness()
    {
        DescriptorTableInstallReadinessResult missingTss =
            DescriptorTableInstallReadiness.RequireReady(
                IdtTablePlanResult.Success(),
                CreateIdtrResult(),
                CreateGdtResult(),
                TssIstPlanResult.Fail(
                    TssIstPlanStatus.InvalidIstIndex,
                    istIndex: 0,
                    actualBytes: TssIstPlan.MinimumTssByteLength),
                doubleFaultIstReady: true,
                GdtDescriptorPlan.TssSelector,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);

        DescriptorTableInstallReadinessResult missingDoubleFaultIst =
            DescriptorTableInstallReadiness.RequireReady(
                IdtTablePlanResult.Success(),
                CreateIdtrResult(),
                CreateGdtResult(),
                CreateTssIstResult(),
                doubleFaultIstReady: false,
                GdtDescriptorPlan.TssSelector,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);

        return HostCheck.Result(!missingTss.Succeeded
            && missingTss.Status == DescriptorTableInstallReadinessStatus.TssIstNotReady
            && missingTss.TssIstStatus == TssIstPlanStatus.InvalidIstIndex
            && !missingDoubleFaultIst.Succeeded
            && missingDoubleFaultIst.Status == DescriptorTableInstallReadinessStatus.DoubleFaultIstNotReady
            && missingDoubleFaultIst.TssIstStatus == TssIstPlanStatus.Ok);
    }

    [HostTest("descriptor install readiness rejects insufficient stub buffers", HostLane.Fast, 3)]
    private static bool TestRejectsInsufficientStubBuffers()
    {
        DescriptorTableInstallReadinessResult shortLgdt =
            DescriptorTableInstallReadiness.RequireReady(
                IdtTablePlanResult.Success(),
                CreateIdtrResult(),
                CreateGdtResult(),
                CreateTssIstResult(),
                doubleFaultIstReady: true,
                GdtDescriptorPlan.TssSelector,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength - 1,
                DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);

        DescriptorTableInstallReadinessResult invalidSelector =
            DescriptorTableInstallReadiness.RequireReady(
                IdtTablePlanResult.Success(),
                CreateIdtrResult(),
                CreateGdtResult(),
                CreateTssIstResult(),
                doubleFaultIstReady: true,
                tssSelector: 0,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
                DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);

        return HostCheck.Result(!shortLgdt.Succeeded
            && shortLgdt.Status == DescriptorTableInstallReadinessStatus.OutputBufferTooSmall
            && shortLgdt.StubPlanStatus == DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall
            && shortLgdt.OperationKind == DescriptorTableInstructionKind.Lgdt
            && shortLgdt.RequiredBytes == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && !invalidSelector.Succeeded
            && invalidSelector.Status == DescriptorTableInstallReadinessStatus.StubPlanNotReady
            && invalidSelector.StubPlanStatus == DescriptorTableInstallStubPlanStatus.InvalidSelector
            && invalidSelector.OperationKind == DescriptorTableInstructionKind.Ltr
            && invalidSelector.RequiredBytes == DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);
    }

    private static DescriptorTableInstallReadinessResult CreateReadyResult()
    {
        return DescriptorTableInstallReadiness.RequireReady(
            IdtTablePlanResult.Success(),
            CreateIdtrResult(),
            CreateGdtResult(),
            CreateTssIstResult(),
            doubleFaultIstReady: true,
            GdtDescriptorPlan.TssSelector,
            DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
            DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
            DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);
    }

    private static IdtrDescriptorResult CreateIdtrResult()
    {
        return IdtrDescriptor.Create(IdtBaseAddress, IdtTablePlan.EncodedByteLength);
    }

    private static GdtDescriptorPlanResult CreateGdtResult()
    {
        byte[] gdtBytes = new byte[GdtDescriptorPlan.RequiredByteLength];
        return GdtDescriptorPlan.WriteKernelGdt(
            TssBaseAddress,
            TssIstPlan.MinimumTssByteLength - 1U,
            gdtBytes);
    }

    private static TssIstPlanResult CreateTssIstResult()
    {
        byte[] tssBytes = new byte[TssIstPlan.MinimumTssByteLength];
        return TssIstPlan.Write(
            Rsp0StackTop,
            DoubleFaultIstIndex,
            DoubleFaultStackTop,
            tssBytes);
    }
}
