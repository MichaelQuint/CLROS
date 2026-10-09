using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class DiagnosticStubCallableBindingPlanTests
{
    private const nuint RegionBaseAddress = 0x0010_0000;

    [HostTest("diagnostics.stub_callable binding accepts materialized callable targets", HostLane.Fast, 0)]
    private static bool TestAcceptsMaterializedCallableTargets()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);
        DiagnosticStubInvocationReadinessResult invocationReadiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);
        ExecutableStubBundleMaterializerResult materializer = ExecutableStubBundleMaterializerResult.Success();

        DiagnosticStubCallableBindingPlanResult result =
            DiagnosticStubCallableBindingPlan.Create(invocationReadiness, backendContract, materializer);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticStubCallableBindingPlanStatus.Ready
            && result.InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && result.MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success
            && result.ConfiguredBasePort == SerialPortConfiguration.Com1BasePort
            && result.PayloadBaseAddress == invocationReadiness.PayloadBaseAddress
            && result.PayloadEndAddress == invocationReadiness.PayloadEndAddress
            && result.PayloadByteLength == invocationReadiness.PayloadEndAddress - invocationReadiness.PayloadBaseAddress
            && result.Out8StubAddress == backendContract.Out8StubAddress
            && result.In8StubAddress == backendContract.In8StubAddress
            && result.Out8StubByteLength == PortIoStubPlan.Out8RequiredByteLength
            && result.In8StubByteLength == PortIoStubPlan.In8RequiredByteLength
            && result.Out8BytesCopied == PortIoStubPlan.Out8RequiredByteLength
            && result.In8BytesCopied == PortIoStubPlan.In8RequiredByteLength
            && !result.CreatesManagedDelegate
            && !result.InvokesCallable
            && !result.PerformsPortIo
            && !result.WritesSerial);
    }

    [HostTest("diagnostics.stub_callable binding rejects failed invocation readiness", HostLane.Fast, 1)]
    private static bool TestRejectsFailedInvocationReadiness()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        DiagnosticStubInvocationReadinessResult invocationReadiness = default;
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);
        ExecutableStubBundleMaterializerResult materializer = ExecutableStubBundleMaterializerResult.Success();

        DiagnosticStubCallableBindingPlanResult result =
            DiagnosticStubCallableBindingPlan.Create(invocationReadiness, backendContract, materializer);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticStubCallableBindingPlanStatus.InvocationReadinessNotReady
            && result.InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success);
    }

    [HostTest("diagnostics.stub_callable binding rejects backend mismatch", HostLane.Fast, 2)]
    private static bool TestRejectsBackendMismatch()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);
        DiagnosticStubInvocationReadinessResult invocationReadiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);
        Com1PortIoBackendContractResult mismatchedContract =
            Com1PortIoBackendContractResult.Success(
                SerialPortConfiguration.Com1BasePort,
                backendContract.Out8StubOffset,
                backendContract.In8StubOffset,
                backendContract.Out8StubAddress,
                backendContract.In8StubAddress + 16);

        DiagnosticStubCallableBindingPlanResult result =
            DiagnosticStubCallableBindingPlan.Create(
                invocationReadiness,
                mismatchedContract,
                ExecutableStubBundleMaterializerResult.Success());

        return HostCheck.Result(!result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanStatus.BackendContractMismatch
            && result.In8StubAddress == mismatchedContract.In8StubAddress);
    }

    [HostTest("diagnostics.stub_callable binding rejects invalid com1 base port", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidCom1BasePort()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract =
            CreateSyntheticBackendContract(
                configuredBasePort: 0x02f8,
                out8StubOffset: 0,
                in8StubOffset: 16,
                out8Address: pageTransition.PayloadBaseAddress,
                in8Address: pageTransition.PayloadBaseAddress + 16);
        DiagnosticStubInvocationReadinessResult invocationReadiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        DiagnosticStubCallableBindingPlanResult result =
            DiagnosticStubCallableBindingPlan.Create(
                invocationReadiness,
                backendContract,
                ExecutableStubBundleMaterializerResult.Success());

        return HostCheck.Result(invocationReadiness.Succeeded
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanStatus.InvalidCom1BasePort
            && result.ConfiguredBasePort == 0x02f8);
    }

    [HostTest("diagnostics.stub_callable binding rejects target beyond materialized payload", HostLane.Fast, 4)]
    private static bool TestRejectsTargetBeyondPayload()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        nuint out8Offset = pageTransition.PayloadByteLength - 1;
        Com1PortIoBackendContractResult backendContract =
            CreateSyntheticBackendContract(
                SerialPortConfiguration.Com1BasePort,
                out8Offset,
                in8StubOffset: 16,
                out8Address: pageTransition.PayloadEndAddress - 1,
                in8Address: pageTransition.PayloadBaseAddress + 16);
        DiagnosticStubInvocationReadinessResult invocationReadiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        DiagnosticStubCallableBindingPlanResult result =
            DiagnosticStubCallableBindingPlan.Create(
                invocationReadiness,
                backendContract,
                ExecutableStubBundleMaterializerResult.Success());

        return HostCheck.Result(invocationReadiness.Succeeded
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanStatus.Out8CallableOutsidePayload
            && result.Out8StubAddress == pageTransition.PayloadEndAddress - 1);
    }

    [HostTest("diagnostics.stub_callable binding rejects overlapping targets", HostLane.Fast, 5)]
    private static bool TestRejectsOverlappingTargets()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract =
            CreateSyntheticBackendContract(
                SerialPortConfiguration.Com1BasePort,
                out8StubOffset: 0,
                in8StubOffset: 3,
                out8Address: pageTransition.PayloadBaseAddress,
                in8Address: pageTransition.PayloadBaseAddress + 3);
        DiagnosticStubInvocationReadinessResult invocationReadiness =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        DiagnosticStubCallableBindingPlanResult result =
            DiagnosticStubCallableBindingPlan.Create(
                invocationReadiness,
                backendContract,
                ExecutableStubBundleMaterializerResult.Success());

        return HostCheck.Result(invocationReadiness.Succeeded
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanStatus.CallableRangesOverlap
            && result.Out8StubAddress == pageTransition.PayloadBaseAddress
            && result.In8StubAddress == pageTransition.PayloadBaseAddress + 3);
    }

    [HostTest("diagnostics.stub_callable binding default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        DiagnosticStubCallableBindingPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubCallableBindingPlanStatus.Ready
            && result.InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && result.MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success
            && result.PayloadBaseAddress == 0
            && result.PayloadEndAddress == 0
            && result.PayloadByteLength == 0
            && result.BytesMaterialized == 0
            && result.Out8StubAddress == 0
            && result.In8StubAddress == 0
            && !result.CreatesManagedDelegate
            && !result.InvokesCallable
            && !result.PerformsPortIo
            && !result.WritesSerial);
    }

    [HostTest("diagnostics.stub_callable binding result contains no managed references", HostLane.Fast, 7)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<DiagnosticStubCallableBindingPlanResult>());
    }

    private static ExecutableStubPageTransitionPlanResult CreateReadyPageTransition()
    {
        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        return ExecutableStubPageTransitionPlan.Create(
            memoryPlan,
            ExecutableStubBundleMaterializerResult.Success(),
            RegionBaseAddress,
            memoryPlan.RequiredByteLength);
    }

    private static Com1PortIoBackendContractResult CreateReadyBackendContract(
        ExecutableStubPageTransitionPlanResult pageTransition)
    {
        Com1PortIoBackendPlanResult backendReadiness =
            Com1PortIoBackendPlan.CreateReady().Validate();
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength)
        };
        ExecutableStubBundlePlanResult bundlePlan = ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(64));

        return Com1PortIoBackendContract.Resolve(
            backendReadiness,
            bundlePlan,
            pageTransition.PayloadBaseAddress);
    }

    private static Com1PortIoBackendContractResult CreateSyntheticBackendContract(
        ushort configuredBasePort,
        nuint out8StubOffset,
        nuint in8StubOffset,
        nuint out8Address,
        nuint in8Address)
    {
        return Com1PortIoBackendContractResult.Success(
            configuredBasePort,
            out8StubOffset,
            in8StubOffset,
            out8Address,
            in8Address);
    }
}
