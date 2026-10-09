using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class OwnedCom1DiagnosticsReadinessTests
{
    private const nuint PayloadBaseAddress = 0x0010_0000;

    [HostTest("diagnostics.owned com1 readiness composes", HostLane.Fast, 0)]
    private static bool TestComposesReadiness()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        OwnedCom1DiagnosticsReadinessResult result =
            OwnedCom1DiagnosticsReadiness.ComposeAndRun(
                Com1PortIoBackendPlan.CreateReady(),
                CreateCom1StubBundlePlan(),
                PayloadBaseAddress,
                guard,
                serial,
                events);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == OwnedCom1DiagnosticsReadinessStatus.Success
            && result.BackendPlan.Status == Com1PortIoBackendPlanStatus.Success
            && result.BackendContract.Status == Com1PortIoBackendContractStatus.Success
            && result.OwnedSerialReadiness.Status == OwnedSerialDiagnosticsReadinessStatus.Success
            && result.OwnedSerialReadiness.HasResolvedBackendAddresses
            && result.MarkOwnedSerialSucceeded
            && result.MarkOwnedSerialStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.PostFirmwareDiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.PostFirmwareMarkerInvoked
            && result.PostFirmwareMarkerSucceeded
            && guard.OwnedSerialReady
            && guard.CanUsePostFirmwareDiagnostics
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.PostFirmwareDiagnosticsReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.PostFirmwareDiagnosticsReady
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedCom1DiagnosticsReadinessResult>());
    }

    [HostTest("diagnostics.owned com1 readiness rejects backend plan", HostLane.Fast, 1)]
    private static bool TestRejectsBackendPlan()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        OwnedCom1DiagnosticsReadinessResult result =
            OwnedCom1DiagnosticsReadiness.ComposeAndRun(
                CreateFailedBackendPlan(),
                CreateCom1StubBundlePlan(),
                PayloadBaseAddress,
                guard,
                serial,
                events);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OwnedCom1DiagnosticsReadinessStatus.BackendPlanFailed
            && result.BackendPlan.Status == Com1PortIoBackendPlanStatus.InvalidSerialConfiguration
            && result.BackendContract.Status == Com1PortIoBackendContractStatus.BackendReadinessFailed
            && result.OwnedSerialReadiness.Status == OwnedSerialDiagnosticsReadinessStatus.BackendPlanFailed
            && !result.MarkOwnedSerialSucceeded
            && !result.PostFirmwareMarkerInvoked
            && serial.Count == 0
            && events.Snapshot().Length == 0
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics);
    }

    [HostTest("diagnostics.owned com1 readiness rejects backend contract", HostLane.Fast, 2)]
    private static bool TestRejectsBackendContract()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        OwnedCom1DiagnosticsReadinessResult result =
            OwnedCom1DiagnosticsReadiness.ComposeAndRun(
                Com1PortIoBackendPlan.CreateReady(),
                CreateCom1StubBundlePlan(),
                executablePayloadBaseAddress: 0,
                guard,
                serial,
                events);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OwnedCom1DiagnosticsReadinessStatus.BackendContractFailed
            && result.BackendPlan.Status == Com1PortIoBackendPlanStatus.Success
            && result.BackendContract.Status == Com1PortIoBackendContractStatus.InvalidPayloadBaseAddress
            && result.OwnedSerialReadiness.Status == OwnedSerialDiagnosticsReadinessStatus.BackendContractFailed
            && !result.OwnedSerialReadiness.HasResolvedBackendAddresses
            && !result.MarkOwnedSerialSucceeded
            && !result.PostFirmwareMarkerInvoked
            && serial.Count == 0
            && events.Snapshot().Length == 0
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics);
    }

    [HostTest("diagnostics.owned com1 readiness emits post firmware marker metadata", HostLane.Fast, 3)]
    private static bool TestEmitsPostFirmwareMarkerMetadata()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkFirmwareServicesUnavailable();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        OwnedCom1DiagnosticsReadinessResult result =
            OwnedCom1DiagnosticsReadiness.ComposeAndRun(
                Com1PortIoBackendPlan.CreateReady(),
                CreateCom1StubBundlePlan(),
                PayloadBaseAddress,
                guard,
                serial,
                events);
        string expectedMarker = BootSequence.CreatePostFirmwareDiagnosticsBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable);
        string[] eventSnapshot = events.Snapshot();

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OwnedCom1DiagnosticsReadinessStatus.GuardTransitionFailed
            && result.BackendPlan.Status == Com1PortIoBackendPlanStatus.Success
            && result.BackendContract.Status == Com1PortIoBackendContractStatus.Success
            && result.OwnedSerialReadiness.Status == OwnedSerialDiagnosticsReadinessStatus.Success
            && !result.MarkOwnedSerialSucceeded
            && result.MarkOwnedSerialStatus == SerialDiagnosticsGuardStatus.InvalidTransition
            && result.PostFirmwareDiagnosticsStatus == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && result.PostFirmwareMarkerInvoked
            && !result.PostFirmwareMarkerSucceeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics);
    }

    [HostTest("diagnostics.owned com1 readiness default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OwnedCom1DiagnosticsReadinessResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == OwnedCom1DiagnosticsReadinessStatus.Success
            && !result.BackendPlan.Succeeded
            && !result.BackendContract.Succeeded
            && !result.OwnedSerialReadiness.Succeeded
            && result.MarkOwnedSerialStatus == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && result.PostFirmwareDiagnosticsStatus == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && !result.MarkOwnedSerialSucceeded
            && !result.PostFirmwareMarkerInvoked
            && !result.PostFirmwareMarkerSucceeded);
    }

    private static Com1PortIoBackendPlan CreateFailedBackendPlan()
    {
        return new Com1PortIoBackendPlan(
            default,
            PortIoOperationPlan.Out8,
            PortIoOperationPlan.In8,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            ExecutableStubMemoryPlan.Create(
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment));
    }

    private static ExecutableStubBundlePlanResult CreateCom1StubBundlePlan()
    {
        Span<ExecutableStubBundleSlot> slots = stackalloc ExecutableStubBundleSlot[]
        {
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1Out8, PortIoStubPlan.Out8RequiredByteLength),
            new ExecutableStubBundleSlot(ExecutableStubSlotKind.Com1In8, PortIoStubPlan.In8RequiredByteLength)
        };

        return ExecutableStubBundlePlan.Create(
            slots,
            ExecutableStubMemoryPlan.Create(64));
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
    }
}
