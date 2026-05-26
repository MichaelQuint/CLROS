using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class DiagnosticStubInvocationReadinessTests
{
    private const nuint RegionBaseAddress = 0x0010_0000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.stub_invocation readiness accepts valid region and addresses", TestAcceptsValidRegionAndAddresses, ref failures);
        Run("diagnostics.stub_invocation readiness rejects failed page transition", TestRejectsFailedPageTransition, ref failures);
        Run("diagnostics.stub_invocation readiness rejects unmaterialized payload", TestRejectsUnmaterializedPayload, ref failures);
        Run("diagnostics.stub_invocation readiness rejects failed backend contract", TestRejectsFailedBackendContract, ref failures);
        Run("diagnostics.stub_invocation readiness rejects out8 address below region", TestRejectsOut8AddressBelowRegion, ref failures);
        Run("diagnostics.stub_invocation readiness rejects out8 address at or beyond region end", TestRejectsOut8AddressAtOrBeyondRegionEnd, ref failures);
        Run("diagnostics.stub_invocation readiness rejects in8 address outside region", TestRejectsIn8AddressOutsideRegion, ref failures);
        Run("diagnostics.stub_invocation readiness default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("diagnostics.stub_invocation readiness result contains no managed references", TestResultContainsNoManagedReferences, ref failures);
        Run("boot.sequence stub invocation readiness emits ready marker", TestBootSequenceReadyMarker, ref failures);
        Run("boot.sequence stub invocation readiness emits blocked marker", TestBootSequenceBlockedMarker, ref failures);
        Run("boot.sequence stub invocation readiness leaves default markers unchanged", TestBootSequenceDefaultMarkersUnchanged, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Diagnostic stub invocation readiness host tests failed: " + failures);
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

    private static bool TestAcceptsValidRegionAndAddresses()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateReadyBackendContract(pageTransition);

        DiagnosticStubInvocationReadinessResult result =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticStubInvocationReadinessStatus.Ready
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && result.PayloadBaseAddress == pageTransition.PayloadBaseAddress
            && result.PayloadEndAddress == pageTransition.PayloadEndAddress
            && result.BytesMaterialized == pageTransition.BytesMaterialized
            && result.Out8StubAddress == backendContract.Out8StubAddress
            && result.In8StubAddress == backendContract.In8StubAddress;
    }

    private static bool TestRejectsFailedPageTransition()
    {
        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(0);
        ExecutableStubPageTransitionPlanResult pageTransition =
            ExecutableStubPageTransitionPlan.Create(
                memoryPlan,
                ExecutableStubBundleMaterializerResult.Success(),
                RegionBaseAddress,
                4096);
        Com1PortIoBackendContractResult backendContract = CreateSyntheticBackendContract(
            RegionBaseAddress,
            RegionBaseAddress + 16);

        DiagnosticStubInvocationReadinessResult result =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == DiagnosticStubInvocationReadinessStatus.PageTransitionNotReady
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.InvalidMemoryPlan
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success;
    }

    private static bool TestRejectsUnmaterializedPayload()
    {
        ExecutableStubMemoryPlanResult memoryPlan = ExecutableStubMemoryPlan.Create(512);
        ExecutableStubBundleMaterializerResult materializer =
            ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                ExecutableStubBundlePlanStatus.InvalidMemoryPlan,
                memoryPlan.Status);
        ExecutableStubPageTransitionPlanResult pageTransition =
            ExecutableStubPageTransitionPlan.Create(
                memoryPlan,
                materializer,
                RegionBaseAddress,
                memoryPlan.RequiredByteLength);
        Com1PortIoBackendContractResult backendContract = CreateSyntheticBackendContract(
            RegionBaseAddress,
            RegionBaseAddress + 16);

        DiagnosticStubInvocationReadinessResult result =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return !result.Succeeded
            && result.Status == DiagnosticStubInvocationReadinessStatus.PageTransitionNotReady
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.UnmaterializedPayload
            && result.BytesMaterialized == 0;
    }

    private static bool TestRejectsFailedBackendContract()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = default;

        DiagnosticStubInvocationReadinessResult result =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return !result.Succeeded
            && result.Status == DiagnosticStubInvocationReadinessStatus.BackendContractNotReady
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && result.Out8StubAddress == 0
            && result.In8StubAddress == 0;
    }

    private static bool TestRejectsOut8AddressBelowRegion()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateSyntheticBackendContract(
            pageTransition.PayloadBaseAddress - 1,
            pageTransition.PayloadBaseAddress + 16);

        DiagnosticStubInvocationReadinessResult result =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return !result.Succeeded
            && result.Status == DiagnosticStubInvocationReadinessStatus.Out8AddressOutsideRegion
            && result.Out8StubAddress == pageTransition.PayloadBaseAddress - 1;
    }

    private static bool TestRejectsOut8AddressAtOrBeyondRegionEnd()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult atEndContract = CreateSyntheticBackendContract(
            pageTransition.PayloadEndAddress,
            pageTransition.PayloadBaseAddress + 16);
        Com1PortIoBackendContractResult beyondEndContract = CreateSyntheticBackendContract(
            pageTransition.PayloadEndAddress + 1,
            pageTransition.PayloadBaseAddress + 16);

        DiagnosticStubInvocationReadinessResult atEndResult =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, atEndContract);
        DiagnosticStubInvocationReadinessResult beyondEndResult =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, beyondEndContract);

        return !atEndResult.Succeeded
            && atEndResult.Status == DiagnosticStubInvocationReadinessStatus.Out8AddressOutsideRegion
            && atEndResult.Out8StubAddress == pageTransition.PayloadEndAddress
            && !beyondEndResult.Succeeded
            && beyondEndResult.Status == DiagnosticStubInvocationReadinessStatus.Out8AddressOutsideRegion
            && beyondEndResult.Out8StubAddress == pageTransition.PayloadEndAddress + 1;
    }

    private static bool TestRejectsIn8AddressOutsideRegion()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        Com1PortIoBackendContractResult backendContract = CreateSyntheticBackendContract(
            pageTransition.PayloadBaseAddress,
            pageTransition.PayloadEndAddress);

        DiagnosticStubInvocationReadinessResult result =
            DiagnosticStubInvocationReadiness.RequireReady(pageTransition, backendContract);

        return !result.Succeeded
            && result.Status == DiagnosticStubInvocationReadinessStatus.In8AddressOutsideRegion
            && result.In8StubAddress == pageTransition.PayloadEndAddress;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        DiagnosticStubInvocationReadinessResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == DiagnosticStubInvocationReadinessStatus.Ready
            && result.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success
            && result.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && result.PayloadBaseAddress == 0
            && result.PayloadEndAddress == 0
            && result.BytesMaterialized == 0
            && result.Out8StubAddress == 0
            && result.In8StubAddress == 0;
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<DiagnosticStubInvocationReadinessResult>();
    }

    private static bool TestBootSequenceReadyMarker()
    {
        DiagnosticStubInvocationReadinessResult readiness = CreateReadyInvocationReadiness();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunDiagnosticStubInvocationReadiness(readiness, serial, events);

        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.DiagnosticStubInvocationReady
            && events.Snapshot()[0] == BootSequence.DiagnosticStubInvocationReady;
    }

    private static bool TestBootSequenceBlockedMarker()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        DiagnosticStubInvocationReadinessResult readiness =
            DiagnosticStubInvocationReadiness.RequireReady(
                pageTransition,
                CreateSyntheticBackendContract(
                    pageTransition.PayloadBaseAddress,
                    pageTransition.PayloadEndAddress));
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateDiagnosticStubInvocationNotReadyMarker(readiness);

        BootResult result = BootSequence.RunDiagnosticStubInvocationReadiness(readiness, serial, events);

        return !result.Succeeded
            && result.Reason.Contains(nameof(DiagnosticStubInvocationReadinessStatus.In8AddressOutsideRegion), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestBootSequenceDefaultMarkersUnchanged()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.DiagnosticStubInvocationReady)
            && !serial.ContainsPrefix(BootSequence.DiagnosticStubInvocationNotReadyPrefix);
    }

    private static DiagnosticStubInvocationReadinessResult CreateReadyInvocationReadiness()
    {
        ExecutableStubPageTransitionPlanResult pageTransition = CreateReadyPageTransition();
        return DiagnosticStubInvocationReadiness.RequireReady(
            pageTransition,
            CreateReadyBackendContract(pageTransition));
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
        nuint out8Address,
        nuint in8Address)
    {
        return Com1PortIoBackendContractResult.Success(
            SerialPortConfiguration.Com1BasePort,
            out8StubOffset: 0,
            in8StubOffset: 16,
            out8Address,
            in8Address);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string value)
        {
            return _lines.Contains(value);
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
