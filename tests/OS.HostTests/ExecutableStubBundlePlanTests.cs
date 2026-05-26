using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class ExecutableStubBundlePlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "executable stub bundle plan packs diagnostics stubs",
            TestPacksDiagnosticsStubs,
            ref failures);
        Run(
            "executable stub bundle plan calculates aligned slot offsets",
            TestCalculatesAlignedSlotOffsets,
            ref failures);
        Run(
            "executable stub bundle plan rejects duplicate or unknown slots",
            TestRejectsDuplicateOrUnknownSlots,
            ref failures);
        Run(
            "executable stub bundle plan rejects insufficient memory plan",
            TestRejectsInsufficientMemoryPlan,
            ref failures);
        Run(
            "executable stub bundle plan default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Executable stub bundle plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

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

        return result.Succeeded
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
            && installGdtOffset == expectedInstallGdt;
    }

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

        return result.Succeeded
            && result.SlotCount == 3
            && result.TotalPayloadLength == 33
            && result.Com1Out8Offset == 0
            && result.Com1In8Offset == 8
            && result.InvalidOpcodeProbeOffset == 32
            && result.InstallIdtOffset == ExecutableStubBundlePlan.MissingSlotOffset
            && result.RequiredMemoryBytes == 4096
            && !result.TryGetSlotOffset(ExecutableStubSlotKind.InstallIdt, out _);
    }

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

        return !duplicateResult.Succeeded
            && duplicateResult.Status == ExecutableStubBundlePlanStatus.DuplicateSlot
            && !unknownResult.Succeeded
            && unknownResult.Status == ExecutableStubBundlePlanStatus.UnknownSlotKind
            && !zeroLengthResult.Succeeded
            && zeroLengthResult.Status == ExecutableStubBundlePlanStatus.ZeroLengthSlot;
    }

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

        return !invalidMemoryPlan.Succeeded
            && invalidMemoryPlan.IsInitialized
            && invalidMemoryPlan.Status == ExecutableStubBundlePlanStatus.InvalidMemoryPlan
            && invalidMemoryPlan.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && !insufficientMemoryPlan.Succeeded
            && insufficientMemoryPlan.Status == ExecutableStubBundlePlanStatus.InsufficientMemoryPlan
            && insufficientMemoryPlan.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && insufficientMemoryPlan.TotalPayloadLength == 5000
            && insufficientMemoryPlan.RequiredMemoryBytes == 8192;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ExecutableStubBundlePlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == ExecutableStubBundlePlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.Com1Out8Offset == 0
            && !result.TryGetSlotOffset(ExecutableStubSlotKind.Com1Out8, out _)
            && !result.TryGetSlotOffset(ExecutableStubSlotKind.InvalidOpcodeProbe, out _);
    }
}
