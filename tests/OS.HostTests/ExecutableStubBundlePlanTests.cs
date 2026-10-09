using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class ExecutableStubBundlePlanTests
{

    [HostTest("executable stub bundle plan packs diagnostics stubs", HostLane.Fast, 0)]
    private static bool TestPacksDiagnosticsStubs()
    {
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InstallIdt, DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InstallGdt, DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.LoadTaskRegister, DescriptorTableInstallStubPlan.LtrStubRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InvalidOpcodeProbe, FaultProbeStubPlan.InvalidOpcodeRequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.NullWriteProbe, FaultProbeStubPlan.NullWriteRequiredByteLength)
        };

        ExecutableStubBundlePlanResult result = ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(512));

        nuint expectedOut8 = 0;
        nuint expectedIn8 = 16;
        nuint expectedInstallIdt = 32;
        nuint expectedInstallGdt = 64;
        nuint expectedLoadTaskRegister = 96;
        nuint expectedInvalidOpcodeProbe = 112;
        nuint expectedNullWriteProbe = 128;
        nuint expectedTotalPayloadLength = 137;

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == ExecutableStubBundlePlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.SlotCount == ExecutableStubBundlePlan.StableSlotCount
            && result.TotalPayloadLength == expectedTotalPayloadLength
            && result.RequiredMemoryBytes == 4096
            && result.Com1Out8Offset == expectedOut8
            && result.Com1In8Offset == expectedIn8
            && result.InstallIdtOffset == expectedInstallIdt
            && result.InstallGdtOffset == expectedInstallGdt
            && result.LoadTaskRegisterOffset == expectedLoadTaskRegister
            && result.InvalidOpcodeProbeOffset == expectedInvalidOpcodeProbe
            && result.NullWriteProbeOffset == expectedNullWriteProbe
            && result.TryGetSlotOffset(ExecutableStubSlotKind.InstallGdt, out nuint installGdtOffset)
            && installGdtOffset == expectedInstallGdt);
    }

    [HostTest("executable stub bundle plan calculates aligned slot offsets", HostLane.Fast, 1)]
    private static bool TestCalculatesAlignedSlotOffsets()
    {
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, 3),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, 17),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.InvalidOpcodeProbe, 1)
        };

        ExecutableStubBundlePlanResult result = ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(
                payloadLength: 128,
                pageSize: 4096,
                alignment: 8));

        return HostCheck.Result(result.Succeeded
            && result.SlotCount == 3
            && result.TotalPayloadLength == 33
            && result.Com1Out8Offset == 0
            && result.Com1In8Offset == 8
            && result.InvalidOpcodeProbeOffset == 32
            && result.InstallIdtOffset == ExecutableStubBundlePlan.MissingSlotOffset
            && result.RequiredMemoryBytes == 4096
            && !result.TryGetSlotOffset(ExecutableStubSlotKind.InstallIdt, out _));
    }

    [HostTest("executable stub bundle plan rejects duplicate or unknown slots", HostLane.Fast, 2)]
    private static bool TestRejectsDuplicateOrUnknownSlots()
    {
        Span<ExecutableStubBundleSlot> duplicateSlots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, 1),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, 1)
        };
        Span<ExecutableStubBundleSlot> unknownSlots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, 1),
            new ExecutableStubBundleSlot((ExecutableStubSlotKind)255, 1)
        };
        Span<ExecutableStubBundleSlot> zeroLengthSlots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, 0)
        };

        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(128);
        ExecutableStubBundlePlanResult duplicateResult =
            ExecutableStubBundlePlan.Create(duplicateSlots, memoryPlan);
        ExecutableStubBundlePlanResult unknownResult =
            ExecutableStubBundlePlan.Create(unknownSlots, memoryPlan);
        ExecutableStubBundlePlanResult zeroLengthResult =
            ExecutableStubBundlePlan.Create(zeroLengthSlots, memoryPlan);

        return HostCheck.Result(!duplicateResult.Succeeded
            && duplicateResult.Status == ExecutableStubBundlePlanStatus.DuplicateSlot
            && !unknownResult.Succeeded
            && unknownResult.Status == ExecutableStubBundlePlanStatus.UnknownSlotKind
            && !zeroLengthResult.Succeeded
            && zeroLengthResult.Status == ExecutableStubBundlePlanStatus.ZeroLengthSlot);
    }

    [HostTest("executable stub bundle plan rejects insufficient memory plan", HostLane.Fast, 3)]
    private static bool TestRejectsInsufficientMemoryPlan()
    {
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, 5000)
        };

        ExecutableStubBundlePlanResult invalidMemoryPlan = ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(0));
        ExecutableStubBundlePlanResult insufficientMemoryPlan = ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(16));

        return HostCheck.Result(!invalidMemoryPlan.Succeeded
            && invalidMemoryPlan.IsInitialized
            && invalidMemoryPlan.Status == ExecutableStubBundlePlanStatus.InvalidMemoryPlan
            && invalidMemoryPlan.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && !insufficientMemoryPlan.Succeeded
            && insufficientMemoryPlan.Status == ExecutableStubBundlePlanStatus.InsufficientMemoryPlan
            && insufficientMemoryPlan.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && insufficientMemoryPlan.TotalPayloadLength == 5000
            && insufficientMemoryPlan.RequiredMemoryBytes == 8192);
    }

    [HostTest("executable stub bundle plan default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        ExecutableStubBundlePlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == ExecutableStubBundlePlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.Com1Out8Offset == 0
            && !result.TryGetSlotOffset(ExecutableStubSlotKind.Com1Out8, out _)
            && !result.TryGetSlotOffset(ExecutableStubSlotKind.InvalidOpcodeProbe, out _));
    }
}
