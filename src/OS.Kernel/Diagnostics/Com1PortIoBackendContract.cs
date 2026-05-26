using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum Com1PortIoBackendContractStatus
{
    Success = 0,
    BackendReadinessFailed = 1,
    StubBundlePlanFailed = 2,
    MissingOut8StubSlot = 3,
    MissingIn8StubSlot = 4,
    InvalidPayloadBaseAddress = 5,
    AddressOverflow = 6,
    InvalidCom1BasePort = 7
}

public readonly struct Com1PortIoBackendContractResult
{
    private Com1PortIoBackendContractResult(
        bool isInitialized,
        Com1PortIoBackendContractStatus status,
        Com1PortIoBackendPlanStatus backendPlanStatus,
        ExecutableStubBundlePlanStatus stubBundlePlanStatus,
        ushort configuredBasePort,
        nuint out8StubOffset,
        nuint in8StubOffset,
        nuint out8StubAddress,
        nuint in8StubAddress)
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

    public static Com1PortIoBackendContractResult Success(
        ushort configuredBasePort,
        nuint out8StubOffset,
        nuint in8StubOffset,
        nuint out8StubAddress,
        nuint in8StubAddress)
    {
        return new Com1PortIoBackendContractResult(
            true,
            Com1PortIoBackendContractStatus.Success,
            Com1PortIoBackendPlanStatus.Success,
            ExecutableStubBundlePlanStatus.Success,
            configuredBasePort,
            out8StubOffset,
            in8StubOffset,
            out8StubAddress,
            in8StubAddress);
    }

    public static Com1PortIoBackendContractResult Fail(
        Com1PortIoBackendContractStatus status,
        Com1PortIoBackendPlanStatus backendPlanStatus,
        ExecutableStubBundlePlanStatus stubBundlePlanStatus,
        ushort configuredBasePort,
        nuint out8StubOffset,
        nuint in8StubOffset)
    {
        return new Com1PortIoBackendContractResult(
            true,
            status,
            backendPlanStatus,
            stubBundlePlanStatus,
            configuredBasePort,
            out8StubOffset,
            in8StubOffset,
            0,
            0);
    }
}

public static class Com1PortIoBackendContract
{
    public const ushort RequiredCom1BasePort = SerialPortConfiguration.Com1BasePort;
    public const int Out8PortParameterBits = 16;
    public const int Out8ValueParameterBits = 8;
    public const int In8PortParameterBits = 16;
    public const int In8ReturnValueBits = 8;

    public static Com1PortIoBackendContractResult Resolve(
        Com1PortIoBackendPlanResult backendReadiness,
        ExecutableStubBundlePlanResult stubBundlePlan,
        nuint executablePayloadBaseAddress)
    {
        return Resolve(
            backendReadiness,
            stubBundlePlan,
            executablePayloadBaseAddress,
            RequiredCom1BasePort);
    }

    public static Com1PortIoBackendContractResult Resolve(
        Com1PortIoBackendPlanResult backendReadiness,
        ExecutableStubBundlePlanResult stubBundlePlan,
        nuint executablePayloadBaseAddress,
        ushort configuredBasePort)
    {
        nuint out8Offset = stubBundlePlan.Com1Out8Offset;
        nuint in8Offset = stubBundlePlan.Com1In8Offset;

        if (!backendReadiness.Succeeded)
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.BackendReadinessFailed,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        if (!stubBundlePlan.Succeeded)
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.StubBundlePlanFailed,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        if (configuredBasePort != RequiredCom1BasePort)
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.InvalidCom1BasePort,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        if (!stubBundlePlan.TryGetSlotOffset(ExecutableStubSlotKind.Com1Out8, out out8Offset))
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.MissingOut8StubSlot,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        if (!stubBundlePlan.TryGetSlotOffset(ExecutableStubSlotKind.Com1In8, out in8Offset))
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.MissingIn8StubSlot,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        if (executablePayloadBaseAddress == 0)
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.InvalidPayloadBaseAddress,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        if (!TryAdd(executablePayloadBaseAddress, out8Offset, out nuint out8Address)
            || !TryAdd(executablePayloadBaseAddress, in8Offset, out nuint in8Address))
        {
            return Com1PortIoBackendContractResult.Fail(
                Com1PortIoBackendContractStatus.AddressOverflow,
                backendReadiness.Status,
                stubBundlePlan.Status,
                configuredBasePort,
                out8Offset,
                in8Offset);
        }

        return Com1PortIoBackendContractResult.Success(
            configuredBasePort,
            out8Offset,
            in8Offset,
            out8Address,
            in8Address);
    }

    private static bool TryAdd(nuint baseAddress, nuint offset, out nuint address)
    {
        address = baseAddress + offset;
        return address >= baseAddress;
    }
}
