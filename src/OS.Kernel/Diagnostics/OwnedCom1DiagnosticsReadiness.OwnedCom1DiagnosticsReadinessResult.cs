using OS.Boot;
using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public readonly struct OwnedCom1DiagnosticsReadinessResult
{
    private OwnedCom1DiagnosticsReadinessResult(bool isInitialized, OwnedCom1DiagnosticsReadinessStatus status, Com1PortIoBackendPlanResult backendPlan, Com1PortIoBackendContractResult backendContract, OwnedSerialDiagnosticsReadinessResult ownedSerialReadiness, SerialDiagnosticsGuardStatus markOwnedSerialStatus, SerialDiagnosticsGuardStatus postFirmwareDiagnosticsStatus, bool markOwnedSerialSucceeded, bool postFirmwareMarkerInvoked, bool postFirmwareMarkerSucceeded)
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
    public bool Succeeded => IsInitialized && Status == OwnedCom1DiagnosticsReadinessStatus.Success && BackendPlan.Succeeded && BackendContract.Succeeded && OwnedSerialReadiness.Succeeded && MarkOwnedSerialSucceeded && MarkOwnedSerialStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady && PostFirmwareDiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady && PostFirmwareMarkerInvoked && PostFirmwareMarkerSucceeded;
    public Com1PortIoBackendPlanResult BackendPlan { get; }
    public Com1PortIoBackendContractResult BackendContract { get; }
    public OwnedSerialDiagnosticsReadinessResult OwnedSerialReadiness { get; }
    public SerialDiagnosticsGuardStatus MarkOwnedSerialStatus { get; }
    public SerialDiagnosticsGuardStatus PostFirmwareDiagnosticsStatus { get; }
    public bool MarkOwnedSerialSucceeded { get; }
    public bool PostFirmwareMarkerInvoked { get; }
    public bool PostFirmwareMarkerSucceeded { get; }

    public static OwnedCom1DiagnosticsReadinessResult Create(OwnedCom1DiagnosticsReadinessStatus status, Com1PortIoBackendPlanResult backendPlan, Com1PortIoBackendContractResult backendContract, OwnedSerialDiagnosticsReadinessResult ownedSerialReadiness, SerialDiagnosticsGuardStatus markOwnedSerialStatus, SerialDiagnosticsGuardStatus postFirmwareDiagnosticsStatus, bool markOwnedSerialSucceeded, bool postFirmwareMarkerInvoked, bool postFirmwareMarkerSucceeded)
    {
        return new OwnedCom1DiagnosticsReadinessResult(true, status, backendPlan, backendContract, ownedSerialReadiness, markOwnedSerialStatus, postFirmwareDiagnosticsStatus, markOwnedSerialSucceeded, postFirmwareMarkerInvoked, postFirmwareMarkerSucceeded);
    }
}
