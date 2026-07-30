using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class ManagedX64StubStrategyADRUseExistingAuditedTests
{
    private const string ReadyMarker =
        "CLROS_M02_001_READY strategy=ExistingAuditedBytePlans families=1 " +
        "generatorThreshold=2 generatorEligible=false out8Bytes=7 in8Bytes=5 " +
        "writeThenExecute=true permanentRwx=false";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.001");

        int failures = 0;

        Run(
            "m02.001 selects existing audited plans",
            TestSelectsExistingAuditedPlans,
            ref failures);
        Run(
            "m02.001 verifies exact audited bytes",
            TestVerifiesExactAuditedBytes,
            ref failures);
        Run(
            "m02.001 enables generator only for second family",
            TestEnablesGeneratorOnlyForSecondFamily,
            ref failures);
        Run(
            "m02.001 rejects malformed policies",
            TestRejectsMalformedPolicies,
            ref failures);
        Run(
            "m02.001 default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);
        Run(
            "m02.001 current decision allocates no managed memory",
            TestCurrentDecisionAllocatesNoManagedMemory,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.001 host-tests failures=" + failures);
            throw new InvalidOperationException(
                "Managed x64 stub strategy host tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS m02.001");
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
            Console.WriteLine("TEST_FAIL " + name + " assertion");
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestSelectsExistingAuditedPlans()
    {
        ManagedX64StubStrategyResult result =
            ManagedX64StubStrategyADRUseExistingAudited.ResolveCurrent();

        return result.IsInitialized &&
            result.Succeeded &&
            result.Status == ManagedX64StubStrategyStatus.Success &&
            result.Strategy == ManagedX64StubAuthoringKind.ExistingAuditedBytePlans &&
            result.DemonstratedFamilyCount == 1 &&
            result.GeneratorThreshold == 2 &&
            !result.GeneratorEligible &&
            result.Out8ByteCount == 7 &&
            result.In8ByteCount == 5 &&
            result.WriteThenExecute &&
            !result.PermanentRwx &&
            ManagedX64StubStrategyADRUseExistingAudited.CurrentDemonstratedFamilyCount == 1 &&
            ManagedX64StubStrategyADRUseExistingAudited.GeneratorEligibilityThreshold == 2 &&
            ManagedX64StubStrategyADRUseExistingAudited.RequiresWriteThenExecute &&
            !ManagedX64StubStrategyADRUseExistingAudited.AllowsPermanentRwx;
    }

    private static bool TestVerifiesExactAuditedBytes()
    {
        byte[] out8 = new byte[PortIoStubPlan.Out8RequiredByteLength];
        byte[] in8 = new byte[PortIoStubPlan.In8RequiredByteLength];

        PortIoStubPlanResult out8Result = PortIoStubPlan.Out8.Emit(out8);
        PortIoStubPlanResult in8Result = PortIoStubPlan.In8.Emit(in8);

        return out8Result.Succeeded &&
            in8Result.Succeeded &&
            out8.Length == 7 &&
            out8[0] == 0x66 &&
            out8[1] == 0x89 &&
            out8[2] == 0xca &&
            out8[3] == 0x88 &&
            out8[4] == 0xd0 &&
            out8[5] == 0xee &&
            out8[6] == 0xc3 &&
            in8.Length == 5 &&
            in8[0] == 0x66 &&
            in8[1] == 0x89 &&
            in8[2] == 0xca &&
            in8[3] == 0xec &&
            in8[4] == 0xc3;
    }

    private static bool TestEnablesGeneratorOnlyForSecondFamily()
    {
        ManagedX64StubStrategyResult firstFamily =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 1,
                generatorThreshold: 2,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: false);
        ManagedX64StubStrategyResult syntheticSecondFamily =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 2,
                generatorThreshold: 2,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: false);
        ManagedX64StubStrategyResult raisedThreshold =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 2,
                generatorThreshold: 3,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: false);

        return firstFamily.Succeeded &&
            !firstFamily.GeneratorEligible &&
            firstFamily.Strategy ==
                ManagedX64StubAuthoringKind.ExistingAuditedBytePlans &&
            syntheticSecondFamily.Succeeded &&
            syntheticSecondFamily.GeneratorEligible &&
            syntheticSecondFamily.Strategy ==
                ManagedX64StubAuthoringKind.BuildTimeGenerator &&
            raisedThreshold.Succeeded &&
            !raisedThreshold.GeneratorEligible &&
            raisedThreshold.Strategy ==
                ManagedX64StubAuthoringKind.ExistingAuditedBytePlans;
    }

    private static bool TestRejectsMalformedPolicies()
    {
        ManagedX64StubStrategyResult missingFamily =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 0,
                generatorThreshold: 2,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: false);
        ManagedX64StubStrategyResult lowThreshold =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 1,
                generatorThreshold: 1,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: false);
        ManagedX64StubStrategyResult changedOut8 =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 1,
                generatorThreshold: 2,
                out8ByteCount: 6,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: false);
        ManagedX64StubStrategyResult changedIn8 =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 1,
                generatorThreshold: 2,
                out8ByteCount: 7,
                in8ByteCount: 4,
                writeThenExecute: true,
                permanentRwx: false);
        ManagedX64StubStrategyResult executeWhileWritable =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 1,
                generatorThreshold: 2,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: false,
                permanentRwx: false);
        ManagedX64StubStrategyResult permanentRwx =
            ManagedX64StubStrategyADRUseExistingAudited.Evaluate(
                demonstratedFamilyCount: 1,
                generatorThreshold: 2,
                out8ByteCount: 7,
                in8ByteCount: 5,
                writeThenExecute: true,
                permanentRwx: true);

        return IsFailure(
                missingFamily,
                ManagedX64StubStrategyStatus.MissingDemonstratedFamily) &&
            IsFailure(
                lowThreshold,
                ManagedX64StubStrategyStatus.GeneratorThresholdTooLow) &&
            IsFailure(
                changedOut8,
                ManagedX64StubStrategyStatus.Out8ByteLengthMismatch) &&
            IsFailure(
                changedIn8,
                ManagedX64StubStrategyStatus.In8ByteLengthMismatch) &&
            IsFailure(
                executeWhileWritable,
                ManagedX64StubStrategyStatus.WriteThenExecuteRequired) &&
            IsFailure(
                permanentRwx,
                ManagedX64StubStrategyStatus.PermanentRwxForbidden);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ManagedX64StubStrategyResult result = default;

        return !result.IsInitialized &&
            !result.Succeeded &&
            result.Status == ManagedX64StubStrategyStatus.Success &&
            result.Strategy == ManagedX64StubAuthoringKind.None &&
            result.DemonstratedFamilyCount == 0 &&
            result.GeneratorThreshold == 0 &&
            !result.GeneratorEligible &&
            result.Out8ByteCount == 0 &&
            result.In8ByteCount == 0 &&
            !result.WriteThenExecute &&
            !result.PermanentRwx &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<
                ManagedX64StubStrategyResult>();
    }

    private static bool TestCurrentDecisionAllocatesNoManagedMemory()
    {
        ManagedX64StubStrategyResult warmup =
            ManagedX64StubStrategyADRUseExistingAudited.ResolveCurrent();
        if (!warmup.Succeeded)
        {
            return false;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        ManagedX64StubStrategyResult result = default;

        for (int index = 0; index < 1024; index++)
        {
            result = ManagedX64StubStrategyADRUseExistingAudited.ResolveCurrent();
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return result.Succeeded && after == before;
    }

    private static bool IsFailure(
        ManagedX64StubStrategyResult result,
        ManagedX64StubStrategyStatus expectedStatus)
    {
        return result.IsInitialized &&
            !result.Succeeded &&
            result.Status == expectedStatus &&
            result.Strategy == ManagedX64StubAuthoringKind.None &&
            !result.GeneratorEligible;
    }
}
