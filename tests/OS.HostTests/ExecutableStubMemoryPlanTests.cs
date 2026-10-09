using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;

internal static class ExecutableStubMemoryPlanTests
{

    [HostTest("executable stub memory plan accepts write-then-execute policy", HostLane.Fast, 0)]
    private static bool TestAcceptsWriteThenExecutePolicy()
    {
        ExecutableStubMemoryPlanResult result = ExecutableStubMemoryPlan.Create(64);

        return HostCheck.Result(result.Succeeded
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
            && result.Flags == ExecutableStubMemoryPlanFlags.None);
    }

    [HostTest("executable stub memory plan rejects accidental rwx policy", HostLane.Fast, 1)]
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

        return HostCheck.Result(!rejected.Succeeded
            && rejected.Status == ExecutableStubMemoryPlanStatus.AccidentalWritableExecutable
            && rejected.RequiredByteLength == 0
            && ExecutableStubMemoryPlan.IsWritableExecutable(rwx)
            && explicitException.Succeeded
            && explicitException.RequiredByteLength == 4096);
    }

    [HostTest("executable stub memory plan calculates aligned layout", HostLane.Fast, 2)]
    private static bool TestCalculatesAlignedLayout()
    {
        ExecutableStubMemoryPlanResult result = ExecutableStubMemoryPlan.Create(
            payloadLength: 4097,
            pageSize: 4096,
            alignment: 64);

        return HostCheck.Result(result.Succeeded
            && result.PayloadOffset == 64
            && result.RequiredByteLength == 8192
            && ExecutableStubMemoryPlan.AlignUp(ExecutableStubMemoryPlan.MetadataPrefixByteLength, 64) == 64
            && ExecutableStubMemoryPlan.AlignUp(4161, 4096) == 8192);
    }

    [HostTest("executable stub memory plan rejects invalid inputs", HostLane.Fast, 3)]
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

        return HostCheck.Result(!zeroPayload.Succeeded
            && zeroPayload.IsInitialized
            && zeroPayload.Status == ExecutableStubMemoryPlanStatus.InvalidPayloadLength
            && !smallPage.Succeeded
            && smallPage.Status == ExecutableStubMemoryPlanStatus.InvalidPageSize
            && !nonPowerOfTwoPage.Succeeded
            && nonPowerOfTwoPage.Status == ExecutableStubMemoryPlanStatus.InvalidPageSize
            && !invalidAlignment.Succeeded
            && invalidAlignment.Status == ExecutableStubMemoryPlanStatus.InvalidAlignment
            && !tooLargeAlignment.Succeeded
            && tooLargeAlignment.Status == ExecutableStubMemoryPlanStatus.AlignmentGreaterThanPageSize);
    }

    [HostTest("executable stub memory plan default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        ExecutableStubMemoryPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == ExecutableStubMemoryPlanStatus.Success
            && result.RequestedPayloadLength == 0
            && result.PageSize == 0
            && result.Alignment == 0
            && result.PayloadOffset == 0
            && result.RequiredByteLength == 0);
    }
}
