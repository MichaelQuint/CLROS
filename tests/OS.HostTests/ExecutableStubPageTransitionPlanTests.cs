using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;

internal static class ExecutableStubPageTransitionPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "executable stub page transition plan accepts write-then-execute",
            TestAcceptsWriteThenExecute,
            ref failures);
        Run(
            "executable stub page transition plan rejects rwx default",
            TestRejectsRwxDefault,
            ref failures);
        Run(
            "executable stub page transition plan rejects unmaterialized payload",
            TestRejectsUnmaterializedPayload,
            ref failures);
        Run(
            "executable stub page transition plan rejects failed plans and invalid ranges",
            TestRejectsFailedPlansAndInvalidRanges,
            ref failures);
        Run(
            "executable stub page transition plan default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Executable stub page transition plan host tests failed: " + failures);
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

    private static bool TestAcceptsWriteThenExecute()
    {
        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundleMaterializerResult materializerResult =
            ExecutableStubBundleMaterializerResult.Success();
        const nuint regionBaseAddress = 0x100000;

        ExecutableStubPageTransitionPlanResult result =
            ExecutableStubPageTransitionPlan.Create(
                memoryPlan,
                materializerResult,
                regionBaseAddress,
                memoryPlan.RequiredByteLength);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == ExecutableStubPageTransitionPlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success
            && result.RegionBaseAddress == regionBaseAddress
            && result.RegionByteLength == memoryPlan.RequiredByteLength
            && result.TransitionBaseAddress == regionBaseAddress
            && result.TransitionByteLength == memoryPlan.RequiredByteLength
            && result.TransitionEndAddress == regionBaseAddress + memoryPlan.RequiredByteLength
            && result.PayloadBaseAddress == regionBaseAddress + memoryPlan.PayloadOffset
            && result.PayloadByteLength == memoryPlan.RequestedPayloadLength
            && result.PayloadEndAddress == result.PayloadBaseAddress + memoryPlan.RequestedPayloadLength
            && result.BytesMaterialized == materializerResult.BytesCopied
            && result.WritePhaseProtection == (ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Write)
            && result.ExecutePhaseProtection == (ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute)
            && result.FinalProtection == (ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute);
    }

    private static bool TestRejectsRwxDefault()
    {
        ExecutableStubMemoryProtection rwx =
            ExecutableStubMemoryProtection.Read |
            ExecutableStubMemoryProtection.Write |
            ExecutableStubMemoryProtection.Execute;
        ExecutableStubMemoryProtection rw =
            ExecutableStubMemoryProtection.Read |
            ExecutableStubMemoryProtection.Write;
        ExecutableStubMemoryProtection rx =
            ExecutableStubMemoryProtection.Read |
            ExecutableStubMemoryProtection.Execute;

        ExecutableStubMemoryPlanResult rwxMemoryPlan = ExecutableStubMemoryPlan.Create(
            payloadLength: 512,
            pageSize: 4096,
            alignment: 16,
            writePhaseProtection: rwx,
            executePhaseProtection: rx,
            finalProtection: rx,
            ExecutableStubMemoryPlanFlags.AllowTemporaryWritableExecutableException);
        ExecutableStubMemoryPlanResult defaultProtectionMemoryPlan = ExecutableStubMemoryPlan.Create(
            payloadLength: 512,
            pageSize: 4096,
            alignment: 16,
            writePhaseProtection: rw,
            executePhaseProtection: ExecutableStubMemoryProtection.None,
            finalProtection: rx,
            ExecutableStubMemoryPlanFlags.None);
        ExecutableStubBundleMaterializerResult materializerResult =
            ExecutableStubBundleMaterializerResult.Success();

        ExecutableStubPageTransitionPlanResult rwxResult =
            ExecutableStubPageTransitionPlan.Create(
                rwxMemoryPlan,
                materializerResult,
                0x100000,
                rwxMemoryPlan.RequiredByteLength);
        ExecutableStubPageTransitionPlanResult defaultProtectionResult =
            ExecutableStubPageTransitionPlan.Create(
                defaultProtectionMemoryPlan,
                materializerResult,
                0x101000,
                defaultProtectionMemoryPlan.RequiredByteLength);

        return rwxMemoryPlan.Succeeded
            && defaultProtectionMemoryPlan.Succeeded
            && !rwxResult.Succeeded
            && rwxResult.Status == ExecutableStubPageTransitionPlanStatus.UnsafeProtectionState
            && rwxResult.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && !defaultProtectionResult.Succeeded
            && defaultProtectionResult.Status == ExecutableStubPageTransitionPlanStatus.UnsafeProtectionState
            && defaultProtectionResult.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success;
    }

    private static bool TestRejectsUnmaterializedPayload()
    {
        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundleMaterializerResult unmaterializedResult =
            ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                ExecutableStubBundlePlanStatus.InvalidMemoryPlan,
                memoryPlan.Status);

        ExecutableStubPageTransitionPlanResult result =
            ExecutableStubPageTransitionPlan.Create(
                memoryPlan,
                unmaterializedResult,
                0x100000,
                memoryPlan.RequiredByteLength);

        return !result.Succeeded
            && result.Status == ExecutableStubPageTransitionPlanStatus.UnmaterializedPayload
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.MaterializerStatus == ExecutableStubBundleMaterializerStatus.InvalidBundlePlan
            && result.BytesMaterialized == 0;
    }

    private static bool TestRejectsFailedPlansAndInvalidRanges()
    {
        ExecutableStubBundleMaterializerResult materializerResult =
            ExecutableStubBundleMaterializerResult.Success();
        ExecutableStubMemoryPlanResult failedMemoryPlan = ExecutableStubMemoryPlan.Create(0);
        ExecutableStubMemoryPlanResult validMemoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubMemoryPlanResult undersizedPayloadMemoryPlan = ExecutableStubMemoryPlan.Create(16);

        ExecutableStubPageTransitionPlanResult failedMemoryResult =
            ExecutableStubPageTransitionPlan.Create(
                failedMemoryPlan,
                materializerResult,
                0x100000,
                4096);
        ExecutableStubPageTransitionPlanResult unalignedBaseResult =
            ExecutableStubPageTransitionPlan.Create(
                validMemoryPlan,
                materializerResult,
                0x100001,
                validMemoryPlan.RequiredByteLength);
        ExecutableStubPageTransitionPlanResult tooSmallRegionResult =
            ExecutableStubPageTransitionPlan.Create(
                validMemoryPlan,
                materializerResult,
                0x100000,
                validMemoryPlan.RequiredByteLength - 1);
        ExecutableStubPageTransitionPlanResult undersizedPayloadResult =
            ExecutableStubPageTransitionPlan.Create(
                undersizedPayloadMemoryPlan,
                materializerResult,
                0x101000,
                undersizedPayloadMemoryPlan.RequiredByteLength);
        ExecutableStubPageTransitionPlanResult overflowResult =
            ExecutableStubPageTransitionPlan.Create(
                validMemoryPlan,
                materializerResult,
                nuint.MaxValue - 4095,
                validMemoryPlan.RequiredByteLength);

        return !failedMemoryResult.Succeeded
            && failedMemoryResult.IsInitialized
            && failedMemoryResult.Status == ExecutableStubPageTransitionPlanStatus.InvalidMemoryPlan
            && failedMemoryResult.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && !unalignedBaseResult.Succeeded
            && unalignedBaseResult.Status == ExecutableStubPageTransitionPlanStatus.InvalidRange
            && !tooSmallRegionResult.Succeeded
            && tooSmallRegionResult.Status == ExecutableStubPageTransitionPlanStatus.InvalidRange
            && !undersizedPayloadResult.Succeeded
            && undersizedPayloadResult.Status == ExecutableStubPageTransitionPlanStatus.InvalidRange
            && !overflowResult.Succeeded
            && overflowResult.Status == ExecutableStubPageTransitionPlanStatus.RangeOverflow;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ExecutableStubPageTransitionPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == ExecutableStubPageTransitionPlanStatus.Success
            && result.MemoryPlanStatus == ExecutableStubMemoryPlanStatus.Success
            && result.MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success
            && result.RegionBaseAddress == 0
            && result.TransitionByteLength == 0
            && result.BytesMaterialized == 0
            && result.FinalProtection == ExecutableStubMemoryProtection.None;
    }
}
