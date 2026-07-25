using OS.Boot;
using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum OwnedCom1DiagnosticsReadinessStatus
{
    Success = 0,
    BackendPlanFailed = 1,
    BackendContractFailed = 2,
    OwnedSerialReadinessFailed = 3,
    GuardTransitionFailed = 4,
    PostFirmwareDiagnosticsBlocked = 5,
    PostFirmwareMarkerFailed = 6,
    MissingGuard = 7
}

public readonly struct OwnedCom1DiagnosticsReadinessResult
{
    private OwnedCom1DiagnosticsReadinessResult(
        bool isInitialized,
        OwnedCom1DiagnosticsReadinessStatus status,
        Com1PortIoBackendPlanResult backendPlan,
        Com1PortIoBackendContractResult backendContract,
        OwnedSerialDiagnosticsReadinessResult ownedSerialReadiness,
        SerialDiagnosticsGuardStatus markOwnedSerialStatus,
        SerialDiagnosticsGuardStatus postFirmwareDiagnosticsStatus,
        bool markOwnedSerialSucceeded,
        bool postFirmwareMarkerInvoked,
        bool postFirmwareMarkerSucceeded)
    {
        IsInitialized = isInitialized;
        Status = status;
        BackendPlan = backendPlan;
        BackendContract = backendContract;
        OwnedSerialReadiness = ownedSerialReadiness;
        MarkOwnedSerialStatus = markOwnedSerialStatus;
        PostFirmwareDiagnosticsStatus = postFirmwareDiagnosticsStatus;
        MarkOwnedSerialSucceeded = markOwnedSerialSucceeded;
        PostFirmwareMarkerInvoked = postFirmwareMarkerInvoked;
        PostFirmwareMarkerSucceeded = postFirmwareMarkerSucceeded;
    }

    public bool IsInitialized { get; }

    public OwnedCom1DiagnosticsReadinessStatus Status { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == OwnedCom1DiagnosticsReadinessStatus.Success &&
        BackendPlan.Succeeded &&
        BackendContract.Succeeded &&
        OwnedSerialReadiness.Succeeded &&
        MarkOwnedSerialSucceeded &&
        MarkOwnedSerialStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady &&
        PostFirmwareDiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady &&
        PostFirmwareMarkerInvoked &&
        PostFirmwareMarkerSucceeded;

    public Com1PortIoBackendPlanResult BackendPlan { get; }

    public Com1PortIoBackendContractResult BackendContract { get; }

    public OwnedSerialDiagnosticsReadinessResult OwnedSerialReadiness { get; }

    public SerialDiagnosticsGuardStatus MarkOwnedSerialStatus { get; }

    public SerialDiagnosticsGuardStatus PostFirmwareDiagnosticsStatus { get; }

    public bool MarkOwnedSerialSucceeded { get; }

    public bool PostFirmwareMarkerInvoked { get; }

    public bool PostFirmwareMarkerSucceeded { get; }

    public static OwnedCom1DiagnosticsReadinessResult Create(
        OwnedCom1DiagnosticsReadinessStatus status,
        Com1PortIoBackendPlanResult backendPlan,
        Com1PortIoBackendContractResult backendContract,
        OwnedSerialDiagnosticsReadinessResult ownedSerialReadiness,
        SerialDiagnosticsGuardStatus markOwnedSerialStatus,
        SerialDiagnosticsGuardStatus postFirmwareDiagnosticsStatus,
        bool markOwnedSerialSucceeded,
        bool postFirmwareMarkerInvoked,
        bool postFirmwareMarkerSucceeded)
    {
        return new OwnedCom1DiagnosticsReadinessResult(
            true,
            status,
            backendPlan,
            backendContract,
            ownedSerialReadiness,
            markOwnedSerialStatus,
            postFirmwareDiagnosticsStatus,
            markOwnedSerialSucceeded,
            postFirmwareMarkerInvoked,
            postFirmwareMarkerSucceeded);
    }
}

public static class OwnedCom1DiagnosticsReadiness
{
    public const string PostFirmwareDiagnosticsOperation = "diagnostics.owned.com1.post-firmware";

    public static OwnedCom1DiagnosticsReadinessResult ComposeAndRun(
        Com1PortIoBackendPlan backendPlan,
        ExecutableStubBundlePlanResult stubBundlePlan,
        nuint executablePayloadBaseAddress,
        SerialDiagnosticsGuard guard,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        Com1PortIoBackendPlanResult backendPlanResult = backendPlan.Validate();
        Com1PortIoBackendContractResult backendContract =
            Com1PortIoBackendContract.Resolve(
                backendPlanResult,
                stubBundlePlan,
                executablePayloadBaseAddress);
        OwnedSerialDiagnosticsReadinessResult ownedSerialReadiness =
            OwnedSerialDiagnosticsReadinessResult.FromCom1Backend(
                backendPlanResult,
                backendContract);

        if (!backendPlanResult.Succeeded)
        {
            return CreateWithoutPostFirmwareMarker(
                OwnedCom1DiagnosticsReadinessStatus.BackendPlanFailed,
                backendPlanResult,
                backendContract,
                ownedSerialReadiness);
        }

        if (!backendContract.Succeeded)
        {
            return CreateWithoutPostFirmwareMarker(
                OwnedCom1DiagnosticsReadinessStatus.BackendContractFailed,
                backendPlanResult,
                backendContract,
                ownedSerialReadiness);
        }

        if (!ownedSerialReadiness.Succeeded)
        {
            return CreateWithoutPostFirmwareMarker(
                OwnedCom1DiagnosticsReadinessStatus.OwnedSerialReadinessFailed,
                backendPlanResult,
                backendContract,
                ownedSerialReadiness);
        }

        if (guard is null)
        {
            return CreateWithoutPostFirmwareMarker(
                OwnedCom1DiagnosticsReadinessStatus.MissingGuard,
                backendPlanResult,
                backendContract,
                ownedSerialReadiness);
        }

        SerialDiagnosticsGuardResult markOwnedSerial =
            guard.MarkOwnedSerialReady(ownedSerialReadiness);
        SerialDiagnosticsGuardResult postFirmwareDiagnostics =
            guard.RequirePostFirmwareDiagnostics(PostFirmwareDiagnosticsOperation);
        BootResult postFirmwareMarker = BootSequence.RunPostFirmwareDiagnosticsReadiness(
            postFirmwareDiagnostics,
            serial,
            events);

        OwnedCom1DiagnosticsReadinessStatus status = GetStatus(
            markOwnedSerial,
            postFirmwareDiagnostics,
            postFirmwareMarker);

        return OwnedCom1DiagnosticsReadinessResult.Create(
            status,
            backendPlanResult,
            backendContract,
            ownedSerialReadiness,
            markOwnedSerial.Status,
            postFirmwareDiagnostics.Status,
            markOwnedSerial.Succeeded,
            postFirmwareMarkerInvoked: true,
            postFirmwareMarkerSucceeded: postFirmwareMarker.Succeeded);
    }

    private static OwnedCom1DiagnosticsReadinessResult CreateWithoutPostFirmwareMarker(
        OwnedCom1DiagnosticsReadinessStatus status,
        Com1PortIoBackendPlanResult backendPlan,
        Com1PortIoBackendContractResult backendContract,
        OwnedSerialDiagnosticsReadinessResult ownedSerialReadiness)
    {
        return OwnedCom1DiagnosticsReadinessResult.Create(
            status,
            backendPlan,
            backendContract,
            ownedSerialReadiness,
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly,
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly,
            markOwnedSerialSucceeded: false,
            postFirmwareMarkerInvoked: false,
            postFirmwareMarkerSucceeded: false);
    }

    private static OwnedCom1DiagnosticsReadinessStatus GetStatus(
        SerialDiagnosticsGuardResult markOwnedSerial,
        SerialDiagnosticsGuardResult postFirmwareDiagnostics,
        BootResult postFirmwareMarker)
    {
        if (!markOwnedSerial.Succeeded)
        {
            return OwnedCom1DiagnosticsReadinessStatus.GuardTransitionFailed;
        }

        if (!postFirmwareDiagnostics.PostFirmwareDiagnosticsAvailable)
        {
            return OwnedCom1DiagnosticsReadinessStatus.PostFirmwareDiagnosticsBlocked;
        }

        return postFirmwareMarker.Succeeded
            ? OwnedCom1DiagnosticsReadinessStatus.Success
            : OwnedCom1DiagnosticsReadinessStatus.PostFirmwareMarkerFailed;
    }
}
