using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public static class DiagnosticStubCallableBindingPlan
{
    public static DiagnosticStubCallableBindingPlanResult Create(DiagnosticStubInvocationReadinessResult invocationReadiness, Com1PortIoBackendContractResult backendContract, ExecutableStubBundleMaterializerResult materializerResult)
    {
        if (!invocationReadiness.Succeeded)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.InvocationReadinessNotReady, invocationReadiness, backendContract, materializerResult);
        }

        if (!backendContract.Succeeded)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.BackendContractNotReady, invocationReadiness, backendContract, materializerResult);
        }

        if (backendContract.ConfiguredBasePort != SerialPortConfiguration.Com1BasePort)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.InvalidCom1BasePort, invocationReadiness, backendContract, materializerResult);
        }

        if (!BackendContractMatchesInvocation(invocationReadiness, backendContract))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.BackendContractMismatch, invocationReadiness, backendContract, materializerResult);
        }

        if (!materializerResult.Succeeded)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.MaterializerNotReady, invocationReadiness, backendContract, materializerResult);
        }

        if (materializerResult.Com1Out8BytesCopied != PortIoStubPlan.Out8RequiredByteLength)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.Out8BytesNotMaterialized, invocationReadiness, backendContract, materializerResult);
        }

        if (materializerResult.Com1In8BytesCopied != PortIoStubPlan.In8RequiredByteLength)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.In8BytesNotMaterialized, invocationReadiness, backendContract, materializerResult);
        }

        if (!CallableRangeInsidePayload(backendContract.Out8StubAddress, PortIoStubPlan.Out8RequiredByteLength, invocationReadiness.PayloadBaseAddress, invocationReadiness.PayloadEndAddress))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.Out8CallableOutsidePayload, invocationReadiness, backendContract, materializerResult);
        }

        if (!CallableRangeInsidePayload(backendContract.In8StubAddress, PortIoStubPlan.In8RequiredByteLength, invocationReadiness.PayloadBaseAddress, invocationReadiness.PayloadEndAddress))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.In8CallableOutsidePayload, invocationReadiness, backendContract, materializerResult);
        }

        if (RangesOverlap(backendContract.Out8StubAddress, PortIoStubPlan.Out8RequiredByteLength, backendContract.In8StubAddress, PortIoStubPlan.In8RequiredByteLength))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.CallableRangesOverlap, invocationReadiness, backendContract, materializerResult);
        }

        return DiagnosticStubCallableBindingPlanResult.Create(DiagnosticStubCallableBindingPlanStatus.Ready, invocationReadiness, backendContract, materializerResult);
    }

    private static bool BackendContractMatchesInvocation(DiagnosticStubInvocationReadinessResult invocationReadiness, Com1PortIoBackendContractResult backendContract)
    {
        return invocationReadiness.BackendContractStatus == backendContract.Status && invocationReadiness.Out8StubAddress == backendContract.Out8StubAddress && invocationReadiness.In8StubAddress == backendContract.In8StubAddress && TryAdd(invocationReadiness.PayloadBaseAddress, backendContract.Out8StubOffset, out nuint out8FromOffset) && TryAdd(invocationReadiness.PayloadBaseAddress, backendContract.In8StubOffset, out nuint in8FromOffset) && out8FromOffset == backendContract.Out8StubAddress && in8FromOffset == backendContract.In8StubAddress;
    }

    private static bool CallableRangeInsidePayload(nuint address, int byteLength, nuint payloadBaseAddress, nuint payloadEndAddress)
    {
        return TryAdd(address, (nuint)byteLength, out nuint endAddress) && address >= payloadBaseAddress && endAddress <= payloadEndAddress;
    }

    private static bool RangesOverlap(nuint firstAddress, int firstByteLength, nuint secondAddress, int secondByteLength)
    {
        if (!TryAdd(firstAddress, (nuint)firstByteLength, out nuint firstEnd) || !TryAdd(secondAddress, (nuint)secondByteLength, out nuint secondEnd))
        {
            return true;
        }

        return firstAddress < secondEnd && secondAddress < firstEnd;
    }

    private static bool TryAdd(nuint value, nuint addend, out nuint result)
    {
        result = value + addend;
        return result >= value;
    }
}
