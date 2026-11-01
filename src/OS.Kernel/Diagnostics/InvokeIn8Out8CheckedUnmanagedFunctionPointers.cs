#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;

#endif
namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
#endif
public static unsafe class InvokeIn8Out8CheckedUnmanagedFunctionPointers
{
    public const ushort RequiredCom1BasePort = 0x03f8;
    public const int MaximumCom1RegisterOffset = 7;
#if !NOSTDLIB_EFI
    public static CheckedPortIoFunctionPointerBindingResult Bind(DiagnosticStubCallableBindingPlanResult bindingPlan)
    {
        if (!HasReadyPrerequisites(bindingPlan))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady, CheckedPortIoFunctionPointerBindingRequest.FromBindingPlan(bindingPlan), 0);
        }

        return Bind(CheckedPortIoFunctionPointerBindingRequest.FromBindingPlan(bindingPlan));
    }

#endif
    public static CheckedPortIoFunctionPointerBindingResult Bind(CheckedPortIoFunctionPointerBindingRequest request)
    {
        if (!request.IsInitialized || !request.SourcePlanSucceeded)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady, request, 0);
        }

        if (request.ConfiguredBasePort != RequiredCom1BasePort)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.InvalidConfiguredBasePort, request, 0);
        }

        uint configuredEndPort = (uint)request.ConfiguredBasePort + MaximumCom1RegisterOffset;
        if (configuredEndPort > 0xffffu)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.ConfiguredPortRangeOverflow, request, 0);
        }

        ushort checkedEndPort = (ushort)configuredEndPort;
        if (request.PayloadBaseAddress == 0 || request.PayloadEndAddress <= request.PayloadBaseAddress || request.BytesMaterialized == 0)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.InvalidPayloadRange, request, checkedEndPort);
        }

        if (request.Out8StubByteLength != PortIoStubPlan.Out8RequiredByteLength || request.In8StubByteLength != PortIoStubPlan.In8RequiredByteLength || request.Out8BytesCopied != PortIoStubPlan.Out8RequiredByteLength || request.In8BytesCopied != PortIoStubPlan.In8RequiredByteLength)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.StubBytesNotReady, request, checkedEndPort);
        }

        if (!TryAdd(request.PayloadBaseAddress, request.Out8StubOffset, out nuint out8FromOffset) || request.Out8StubAddress == 0 || out8FromOffset != request.Out8StubAddress)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.Out8TargetMismatch, request, checkedEndPort);
        }

        if (!TryAdd(request.PayloadBaseAddress, request.In8StubOffset, out nuint in8FromOffset) || request.In8StubAddress == 0 || in8FromOffset != request.In8StubAddress)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.In8TargetMismatch, request, checkedEndPort);
        }

        if (!RangeInsidePayload(request.Out8StubAddress, PortIoStubPlan.Out8RequiredByteLength, request.PayloadBaseAddress, request.PayloadEndAddress))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.Out8TargetOutsidePayload, request, checkedEndPort);
        }

        if (!RangeInsidePayload(request.In8StubAddress, PortIoStubPlan.In8RequiredByteLength, request.PayloadBaseAddress, request.PayloadEndAddress))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.In8TargetOutsidePayload, request, checkedEndPort);
        }

        if (RangesOverlap(request.Out8StubAddress, PortIoStubPlan.Out8RequiredByteLength, request.In8StubAddress, PortIoStubPlan.In8RequiredByteLength))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(CheckedPortIoFunctionPointerBindingStatus.CallableRangesOverlap, request, checkedEndPort);
        }

        delegate* unmanaged<ushort, byte, void> out8 = (delegate* unmanaged<ushort, byte, void> )request.Out8StubAddress;
        delegate* unmanaged<ushort, byte> in8 = (delegate* unmanaged<ushort, byte> )request.In8StubAddress;
        return CheckedPortIoFunctionPointerBindingResult.Ready(request, checkedEndPort, out8, in8);
    }

#if !NOSTDLIB_EFI
    private static bool HasReadyPrerequisites(DiagnosticStubCallableBindingPlanResult bindingPlan)
    {
        return bindingPlan.IsInitialized && bindingPlan.Status == DiagnosticStubCallableBindingPlanStatus.Ready && bindingPlan.InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready && bindingPlan.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success && bindingPlan.BackendContractStatus == Com1PortIoBackendContractStatus.Success && bindingPlan.MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success;
    }

#endif
    private static bool RangeInsidePayload(nuint address, int byteLength, nuint payloadBaseAddress, nuint payloadEndAddress)
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
