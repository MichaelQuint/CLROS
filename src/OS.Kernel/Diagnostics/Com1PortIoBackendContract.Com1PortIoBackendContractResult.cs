using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public readonly struct Com1PortIoBackendContractResult
{
    private Com1PortIoBackendContractResult(bool isInitialized, Com1PortIoBackendContractStatus status, Com1PortIoBackendPlanStatus backendPlanStatus, ExecutableStubBundlePlanStatus stubBundlePlanStatus, ushort configuredBasePort, nuint out8StubOffset, nuint in8StubOffset, nuint out8StubAddress, nuint in8StubAddress)
    {
        IsInitialized = isInitialized;
        Status = status;
        BackendPlanStatus = backendPlanStatus;
        StubBundlePlanStatus = stubBundlePlanStatus;
        ConfiguredBasePort = configuredBasePort;
        Out8StubOffset = out8StubOffset;
        In8StubOffset = in8StubOffset;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
    }

    public bool IsInitialized { get; }
    public Com1PortIoBackendContractStatus Status { get; }
    public Com1PortIoBackendPlanStatus BackendPlanStatus { get; }
    public ExecutableStubBundlePlanStatus StubBundlePlanStatus { get; }
    public ushort ConfiguredBasePort { get; }
    public nuint Out8StubOffset { get; }
    public nuint In8StubOffset { get; }
    public nuint Out8StubAddress { get; }
    public nuint In8StubAddress { get; }
    public bool Succeeded => IsInitialized && Status == Com1PortIoBackendContractStatus.Success;

    public static Com1PortIoBackendContractResult Success(ushort configuredBasePort, nuint out8StubOffset, nuint in8StubOffset, nuint out8StubAddress, nuint in8StubAddress)
    {
        return new Com1PortIoBackendContractResult(true, Com1PortIoBackendContractStatus.Success, Com1PortIoBackendPlanStatus.Success, ExecutableStubBundlePlanStatus.Success, configuredBasePort, out8StubOffset, in8StubOffset, out8StubAddress, in8StubAddress);
    }

    public static Com1PortIoBackendContractResult Fail(Com1PortIoBackendContractStatus status, Com1PortIoBackendPlanStatus backendPlanStatus, ExecutableStubBundlePlanStatus stubBundlePlanStatus, ushort configuredBasePort, nuint out8StubOffset, nuint in8StubOffset)
    {
        return new Com1PortIoBackendContractResult(true, status, backendPlanStatus, stubBundlePlanStatus, configuredBasePort, out8StubOffset, in8StubOffset, 0, 0);
    }
}
