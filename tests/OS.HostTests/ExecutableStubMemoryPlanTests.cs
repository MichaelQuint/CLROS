using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;

internal static class ExecutableStubMemoryPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("executable stub memory plan accepts write-then-execute policy", TestAcceptsWriteThenExecutePolicy, ref failures);
        Run("executable stub memory plan rejects accidental rwx policy", TestRejectsAccidentalRwxPolicy, ref failures);
        Run("executable stub memory plan calculates aligned layout", TestCalculatesAlignedLayout, ref failures);
        Run("executable stub memory plan rejects invalid inputs", TestRejectsInvalidInputs, ref failures);
        Run("executable stub memory plan default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Executable stub memory plan host tests failed: " + failures);
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

    private static bool TestAcceptsWriteThenExecutePolicy()
    {
        ExecutableStubMemoryPlanResult result = ExecutableStubMemoryPlan.Create(64);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == ExecutableStubMemoryPlanStatus.Success
            && result.RequestedPayloadLength == 64
            && result.PageSize == ExecutableStubMemoryPlan.DefaultPageSize
            && result.Alignment == ExecutableStubMemoryPlan.DefaultAlignment
            && result.PayloadOffset == 16
            && result.RequiredByteLength == 4096
            && result.WritePhaseProtection == (ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Write)
            && result.ExecutePhaseProtection == (ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute)
            && result.FinalProtection == (ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute)
            && result.Flags == ExecutableStubMemoryPlanFlags.None;
    }

    private static bool TestRejectsAccidentalRwxPolicy()
    {
        ExecutableStubMemoryProtection rwx =
            ExecutableStubMemoryProtection.Read |
            ExecutableStubMemoryProtection.Write |
            ExecutableStubMemoryProtection.Execute;

        ExecutableStubMemoryPlanResult rejected = ExecutableStubMemoryPlan.Create(
            payloadLength: 16,
            pageSize: 4096,
            alignment: 16,
            writePhaseProtection: rwx,
            executePhaseProtection: ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
            finalProtection: ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
            ExecutableStubMemoryPlanFlags.None);

        ExecutableStubMemoryPlanResult explicitException = ExecutableStubMemoryPlan.Create(
            payloadLength: 16,
            pageSize: 4096,
            alignment: 16,
            writePhaseProtection: rwx,
            executePhaseProtection: ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
            finalProtection: ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
            ExecutableStubMemoryPlanFlags.AllowTemporaryWritableExecutableException);

        return !rejected.Succeeded
            && rejected.Status == ExecutableStubMemoryPlanStatus.AccidentalWritableExecutable
            && rejected.RequiredByteLength == 0
            && ExecutableStubMemoryPlan.IsWritableExecutable(rwx)
            && explicitException.Succeeded
            && explicitException.RequiredByteLength == 4096;
    }

    private static bool TestCalculatesAlignedLayout()
    {
        ExecutableStubMemoryPlanResult result = ExecutableStubMemoryPlan.Create(
            payloadLength: 4097,
            pageSize: 4096,
            alignment: 64);

        return result.Succeeded
            && result.PayloadOffset == 64
            && result.RequiredByteLength == 8192
            && ExecutableStubMemoryPlan.AlignUp(ExecutableStubMemoryPlan.MetadataPrefixByteLength, 64) == 64
            && ExecutableStubMemoryPlan.AlignUp(4161, 4096) == 8192;
    }

    private static bool TestRejectsInvalidInputs()
    {
        ExecutableStubMemoryPlanResult zeroPayload = ExecutableStubMemoryPlan.Create(0);
        ExecutableStubMemoryPlanResult smallPage = ExecutableStubMemoryPlan.Create(
            payloadLength: 16,
            pageSize: 2048,
            alignment: 16);
        ExecutableStubMemoryPlanResult nonPowerOfTwoPage = ExecutableStubMemoryPlan.Create(
            payloadLength: 16,
            pageSize: 6144,
            alignment: 16);
        ExecutableStubMemoryPlanResult invalidAlignment = ExecutableStubMemoryPlan.Create(
            payloadLength: 16,
            pageSize: 4096,
            alignment: 24);
        ExecutableStubMemoryPlanResult tooLargeAlignment = ExecutableStubMemoryPlan.Create(
            payloadLength: 16,
            pageSize: 4096,
            alignment: 8192);

        return !zeroPayload.Succeeded
            && zeroPayload.IsInitialized
            && zeroPayload.Status == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && !smallPage.Succeeded
            && smallPage.Status == ExecutableStubMemoryPlanStatus.InvalidPageSize
            && !nonPowerOfTwoPage.Succeeded
            && nonPowerOfTwoPage.Status == ExecutableStubMemoryPlanStatus.InvalidPageSize
            && !invalidAlignment.Succeeded
            && invalidAlignment.Status == ExecutableStubMemoryPlanStatus.InvalidAlignment
            && !tooLargeAlignment.Succeeded
            && tooLargeAlignment.Status == ExecutableStubMemoryPlanStatus.AlignmentGreaterThanPageSize;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ExecutableStubMemoryPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == ExecutableStubMemoryPlanStatus.Success
            && result.RequestedPayloadLength == 0
            && result.PageSize == 0
            && result.Alignment == 0
            && result.PayloadOffset == 0
            && result.RequiredByteLength == 0;
    }
}
