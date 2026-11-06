using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public static class Com1PortIoBackendContract
{
    public const ushort RequiredCom1BasePort = SerialPortConfiguration.Com1BasePort;
    public const int Out8PortParameterBits = 16;
    public const int Out8ValueParameterBits = 8;
    public const int In8PortParameterBits = 16;
    public const int In8ReturnValueBits = 8;
    public static Com1PortIoBackendContractResult Resolve(Com1PortIoBackendPlanResult backendReadiness, ExecutableStubBundlePlanResult stubBundlePlan, nuint executablePayloadBaseAddress)
    {
        return Resolve(backendReadiness, stubBundlePlan, executablePayloadBaseAddress, RequiredCom1BasePort);
    }

    public static Com1PortIoBackendContractResult Resolve(Com1PortIoBackendPlanResult backendReadiness, ExecutableStubBundlePlanResult stubBundlePlan, nuint executablePayloadBaseAddress, ushort configuredBasePort)
    {
        nuint out8Offset = stubBundlePlan.Com1Out8Offset;
        nuint in8Offset = stubBundlePlan.Com1In8Offset;
        if (!backendReadiness.Succeeded)
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.BackendReadinessFailed, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        if (!stubBundlePlan.Succeeded)
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.StubBundlePlanFailed, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        if (configuredBasePort != RequiredCom1BasePort)
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.InvalidCom1BasePort, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        if (!stubBundlePlan.TryGetSlotOffset(ExecutableStubSlotKind.Com1Out8, out out8Offset))
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.MissingOut8StubSlot, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        if (!stubBundlePlan.TryGetSlotOffset(ExecutableStubSlotKind.Com1In8, out in8Offset))
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.MissingIn8StubSlot, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        if (executablePayloadBaseAddress == 0)
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.InvalidPayloadBaseAddress, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        if (!TryAdd(executablePayloadBaseAddress, out8Offset, out nuint out8Address) || !TryAdd(executablePayloadBaseAddress, in8Offset, out nuint in8Address))
        {
            return Com1PortIoBackendContractResult.Fail(Com1PortIoBackendContractStatus.AddressOverflow, backendReadiness.Status, stubBundlePlan.Status, configuredBasePort, out8Offset, in8Offset);
        }

        return Com1PortIoBackendContractResult.Success(configuredBasePort, out8Offset, in8Offset, out8Address, in8Address);
    }

    private static bool TryAdd(nuint baseAddress, nuint offset, out nuint address)
    {
        address = baseAddress + offset;
        return address >= baseAddress;
    }
}
