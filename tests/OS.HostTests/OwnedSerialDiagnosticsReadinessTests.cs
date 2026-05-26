using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static class OwnedSerialDiagnosticsReadinessTests
{
    private const nuint PayloadBaseAddress = 0x0010_0000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "diagnostics owned serial readiness accepts com1 backend contract",
            TestAcceptsCom1BackendContract,
            ref failures);
        Run(
            "diagnostics owned serial readiness rejects failed backend plan",
            TestRejectsFailedBackendPlan,
            ref failures);
        Run(
            "diagnostics owned serial readiness rejects failed backend contract",
            TestRejectsFailedBackendContract,
            ref failures);
        Run(
            "diagnostics owned serial readiness rejects late firmware unavailable",
            TestRejectsLateFirmwareUnavailable,
            ref failures);
        Run(
            "diagnostics owned serial readiness default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Owned serial diagnostics readiness host tests failed: " + failures);
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

    private static bool TestAcceptsCom1BackendContract()
    {
        OwnedSerialDiagnosticsReadinessResult readiness = CreateReadyOwnedSerialReadiness();
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();

        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady(readiness);
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult postFirmware =
            guard.RequirePostFirmwareDiagnostics("post-firmware.panic");

        return readiness.Succeeded
            && readiness.IsInitialized
            && readiness.Status == OwnedSerialDiagnosticsReadinessStatus.Success
            && readiness.BackendPlanStatus == Com1PortIoBackendPlanStatus.Success
            && readiness.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && readiness.StubBundlePlanStatus == ExecutableStubBundlePlanStatus.Success
            && readiness.ConfiguredBasePort == SerialPortConfiguration.Com1BasePort
            && readiness.Out8StubAddress == PayloadBaseAddress
            && readiness.In8StubAddress == PayloadBaseAddress + 16
            && readiness.HasResolvedBackendAddresses
            && ownedReady.Succeeded
            && ownedReady.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyFromCom1Operation
            && ownedReady.Status == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && ownedReady.PostFirmwareDiagnosticsAvailable
            && firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && postFirmware.Succeeded
            && postFirmware.PostFirmwareDiagnosticsAvailable
            && guard.OwnedSerialReady
            && guard.CanUsePostFirmwareDiagnostics;
    }

    private static bool TestRejectsFailedBackendPlan()
    {
        Com1PortIoBackendPlanResult backendReadiness = CreateFailedBackendReadiness();
        Com1PortIoBackendContractResult backendContract = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            CreateCom1StubBundlePlan(),
            PayloadBaseAddress);

        OwnedSerialDiagnosticsReadinessResult readiness =
            OwnedSerialDiagnosticsReadinessResult.FromCom1Backend(
                backendReadiness,
                backendContract);
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady(readiness);

        return !backendReadiness.Succeeded
            && !backendContract.Succeeded
            && readiness.IsInitialized
            && readiness.Status == OwnedSerialDiagnosticsReadinessStatus.BackendPlanFailed
            && readiness.BackendPlanStatus == Com1PortIoBackendPlanStatus.InvalidSerialConfiguration
            && readiness.BackendContractStatus == Com1PortIoBackendContractStatus.BackendReadinessFailed
            && !readiness.HasResolvedBackendAddresses
            && !ownedReady.Succeeded
            && ownedReady.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && ownedReady.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyFromCom1Operation
            && ownedReady.Reason.Contains("BackendPlanFailed", StringComparison.Ordinal)
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics;
    }

    private static bool TestRejectsFailedBackendContract()
    {
        Com1PortIoBackendPlanResult backendReadiness = CreateReadyBackendReadiness();
        Com1PortIoBackendContractResult backendContract = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            CreateCom1StubBundlePlan(),
            0);

        OwnedSerialDiagnosticsReadinessResult readiness =
            OwnedSerialDiagnosticsReadinessResult.FromCom1Backend(
                backendReadiness,
                backendContract);
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady(readiness);

        return backendReadiness.Succeeded
            && !backendContract.Succeeded
            && readiness.IsInitialized
            && readiness.Status == OwnedSerialDiagnosticsReadinessStatus.BackendContractFailed
            && readiness.BackendPlanStatus == Com1PortIoBackendPlanStatus.Success
            && readiness.BackendContractStatus == Com1PortIoBackendContractStatus.InvalidPayloadBaseAddress
            && !readiness.HasResolvedBackendAddresses
            && !ownedReady.Succeeded
            && ownedReady.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && ownedReady.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyFromCom1Operation
            && ownedReady.Reason.Contains("BackendContractFailed", StringComparison.Ordinal)
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics;
    }

    private static bool TestRejectsLateFirmwareUnavailable()
    {
        OwnedSerialDiagnosticsReadinessResult readiness = CreateReadyOwnedSerialReadiness();
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady(readiness);

        return readiness.Succeeded
            && readiness.IsInitialized
            && firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !ownedReady.Succeeded
            && ownedReady.Status == SerialDiagnosticsGuardStatus.InvalidTransition
            && ownedReady.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyFromCom1Operation
            && ownedReady.Reason.Contains("FirmwareServicesUnavailable", StringComparison.Ordinal)
            && guard.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        OwnedSerialDiagnosticsReadinessResult readiness = default;
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady(readiness);

        return !readiness.IsInitialized
            && !readiness.Succeeded
            && !readiness.HasResolvedBackendAddresses
            && readiness.Status == OwnedSerialDiagnosticsReadinessStatus.Success
            && readiness.BackendPlanStatus == Com1PortIoBackendPlanStatus.Success
            && readiness.BackendContractStatus == Com1PortIoBackendContractStatus.Success
            && readiness.StubBundlePlanStatus == ExecutableStubBundlePlanStatus.Success
            && readiness.ConfiguredBasePort == 0
            && readiness.Out8StubAddress == 0
            && readiness.In8StubAddress == 0
            && !ownedReady.Succeeded
            && ownedReady.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && ownedReady.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyFromCom1Operation
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics;
    }

    private static OwnedSerialDiagnosticsReadinessResult CreateReadyOwnedSerialReadiness()
    {
        Com1PortIoBackendPlanResult backendReadiness = CreateReadyBackendReadiness();
        Com1PortIoBackendContractResult backendContract = Com1PortIoBackendContract.Resolve(
            backendReadiness,
            CreateCom1StubBundlePlan(),
            PayloadBaseAddress);

        return OwnedSerialDiagnosticsReadinessResult.FromCom1Backend(
            backendReadiness,
            backendContract);
    }

    private static Com1PortIoBackendPlanResult CreateReadyBackendReadiness()
    {
        return Com1PortIoBackendPlan.CreateReady().Validate();
    }

    private static Com1PortIoBackendPlanResult CreateFailedBackendReadiness()
    {
        Com1PortIoBackendPlan invalidPlan = new Com1PortIoBackendPlan(
            default,
            PortIoOperationPlan.Out8,
            PortIoOperationPlan.In8,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            ExecutableStubMemoryPlan.Create(
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                ExecutableStubMemoryPlan.DefaultPageSize,
                ExecutableStubMemoryPlan.DefaultAlignment));

        return invalidPlan.Validate();
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
}
