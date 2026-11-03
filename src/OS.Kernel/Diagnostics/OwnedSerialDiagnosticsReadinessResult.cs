using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public readonly struct OwnedSerialDiagnosticsReadinessResult
{
    private OwnedSerialDiagnosticsReadinessResult(bool isInitialized, OwnedSerialDiagnosticsReadinessStatus status, Com1PortIoBackendPlanStatus backendPlanStatus, Com1PortIoBackendContractStatus backendContractStatus, ExecutableStubBundlePlanStatus stubBundlePlanStatus, ushort configuredBasePort, nuint out8StubAddress, nuint in8StubAddress)
    {
        IsInitialized = isInitialized;
        Status = status;
        BackendPlanStatus = backendPlanStatus;
        BackendContractStatus = backendContractStatus;
        StubBundlePlanStatus = stubBundlePlanStatus;
        ConfiguredBasePort = configuredBasePort;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
    }

    public bool IsInitialized
    {
        get;
    }
    public OwnedSerialDiagnosticsReadinessStatus Status
    {
        get;
    }
    public Com1PortIoBackendPlanStatus BackendPlanStatus
    {
        get;
    }
    public Com1PortIoBackendContractStatus BackendContractStatus
    {
        get;
    }
    public ExecutableStubBundlePlanStatus StubBundlePlanStatus
    {
        get;
    }
    public ushort ConfiguredBasePort
    {
        get;
    }
    public nuint Out8StubAddress
    {
        get;
    }
    public nuint In8StubAddress
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == OwnedSerialDiagnosticsReadinessStatus.Success && BackendPlanStatus == Com1PortIoBackendPlanStatus.Success && BackendContractStatus == Com1PortIoBackendContractStatus.Success && StubBundlePlanStatus == ExecutableStubBundlePlanStatus.Success && ConfiguredBasePort == Com1PortIoBackendContract.RequiredCom1BasePort && Out8StubAddress != 0 && In8StubAddress != 0;
    public bool HasResolvedBackendAddresses => Succeeded && Out8StubAddress != 0 && In8StubAddress != 0;

    public static OwnedSerialDiagnosticsReadinessResult FromCom1Backend(Com1PortIoBackendPlanResult backendReadiness, Com1PortIoBackendContractResult backendContract)
    {
        if (!backendReadiness.Succeeded)
        {
            return CreateFailure(OwnedSerialDiagnosticsReadinessStatus.BackendPlanFailed, backendReadiness, backendContract);
        }

        if (!backendContract.Succeeded || backendContract.BackendPlanStatus != Com1PortIoBackendPlanStatus.Success || backendContract.StubBundlePlanStatus != ExecutableStubBundlePlanStatus.Success || backendContract.ConfiguredBasePort != Com1PortIoBackendContract.RequiredCom1BasePort || backendContract.Out8StubAddress == 0 || backendContract.In8StubAddress == 0)
        {
            return CreateFailure(OwnedSerialDiagnosticsReadinessStatus.BackendContractFailed, backendReadiness, backendContract);
        }

        return new OwnedSerialDiagnosticsReadinessResult(true, OwnedSerialDiagnosticsReadinessStatus.Success, backendReadiness.Status, backendContract.Status, backendContract.StubBundlePlanStatus, backendContract.ConfiguredBasePort, backendContract.Out8StubAddress, backendContract.In8StubAddress);
    }

    private static OwnedSerialDiagnosticsReadinessResult CreateFailure(OwnedSerialDiagnosticsReadinessStatus status, Com1PortIoBackendPlanResult backendReadiness, Com1PortIoBackendContractResult backendContract)
    {
        return new OwnedSerialDiagnosticsReadinessResult(true, status, backendReadiness.Status, backendContract.Status, backendContract.StubBundlePlanStatus, backendContract.ConfiguredBasePort, backendContract.Out8StubAddress, backendContract.In8StubAddress);
    }
}
