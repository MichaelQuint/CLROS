#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;
#endif

namespace OS.Kernel.Diagnostics;

public enum CheckedPortIoFunctionPointerBindingStatus
{
    Ready = 0,
    BindingPlanNotReady = 1,
    InvalidConfiguredBasePort = 2,
    ConfiguredPortRangeOverflow = 3,
    InvalidPayloadRange = 4,
    StubBytesNotReady = 5,
    Out8TargetMismatch = 6,
    In8TargetMismatch = 7,
    Out8TargetOutsidePayload = 8,
    In8TargetOutsidePayload = 9,
    CallableRangesOverlap = 10
}

public enum CheckedPortIoFunctionPointerInvocationStatus
{
    Success = 0,
    BindingNotReady = 1,
    PortOutsideConfiguredRange = 2
}

public readonly struct CheckedPortIoFunctionPointerBindingRequest
{
    private CheckedPortIoFunctionPointerBindingRequest(
        bool isInitialized,
        bool sourcePlanSucceeded,
        ushort configuredBasePort,
        nuint payloadBaseAddress,
        nuint payloadEndAddress,
        nuint bytesMaterialized,
        nuint out8StubOffset,
        nuint in8StubOffset,
        nuint out8StubAddress,
        nuint in8StubAddress,
        int out8StubByteLength,
        int in8StubByteLength,
        int out8BytesCopied,
        int in8BytesCopied)
    {
        IsInitialized = isInitialized;
        SourcePlanSucceeded = sourcePlanSucceeded;
        ConfiguredBasePort = configuredBasePort;
        PayloadBaseAddress = payloadBaseAddress;
        PayloadEndAddress = payloadEndAddress;
        BytesMaterialized = bytesMaterialized;
        Out8StubOffset = out8StubOffset;
        In8StubOffset = in8StubOffset;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
        Out8StubByteLength = out8StubByteLength;
        In8StubByteLength = in8StubByteLength;
        Out8BytesCopied = out8BytesCopied;
        In8BytesCopied = in8BytesCopied;
    }

    public bool IsInitialized { get; }

    public bool SourcePlanSucceeded { get; }

    public ushort ConfiguredBasePort { get; }

    public nuint PayloadBaseAddress { get; }

    public nuint PayloadEndAddress { get; }

    public nuint BytesMaterialized { get; }

    public nuint Out8StubOffset { get; }

    public nuint In8StubOffset { get; }

    public nuint Out8StubAddress { get; }

    public nuint In8StubAddress { get; }

    public int Out8StubByteLength { get; }

    public int In8StubByteLength { get; }

    public int Out8BytesCopied { get; }

    public int In8BytesCopied { get; }

    public static CheckedPortIoFunctionPointerBindingRequest Create(
        bool sourcePlanSucceeded,
        ushort configuredBasePort,
        nuint payloadBaseAddress,
        nuint payloadEndAddress,
        nuint bytesMaterialized,
        nuint out8StubOffset,
        nuint in8StubOffset,
        nuint out8StubAddress,
        nuint in8StubAddress,
        int out8StubByteLength,
        int in8StubByteLength,
        int out8BytesCopied,
        int in8BytesCopied)
    {
        return new CheckedPortIoFunctionPointerBindingRequest(
            true,
            sourcePlanSucceeded,
            configuredBasePort,
            payloadBaseAddress,
            payloadEndAddress,
            bytesMaterialized,
            out8StubOffset,
            in8StubOffset,
            out8StubAddress,
            in8StubAddress,
            out8StubByteLength,
            in8StubByteLength,
            out8BytesCopied,
            in8BytesCopied);
    }

#if !NOSTDLIB_EFI
    public static CheckedPortIoFunctionPointerBindingRequest FromBindingPlan(
        DiagnosticStubCallableBindingPlanResult bindingPlan)
    {
        return Create(
            bindingPlan.Succeeded,
            bindingPlan.ConfiguredBasePort,
            bindingPlan.PayloadBaseAddress,
            bindingPlan.PayloadEndAddress,
            bindingPlan.BytesMaterialized,
            bindingPlan.Out8StubOffset,
            bindingPlan.In8StubOffset,
            bindingPlan.Out8StubAddress,
            bindingPlan.In8StubAddress,
            bindingPlan.Out8StubByteLength,
            bindingPlan.In8StubByteLength,
            bindingPlan.Out8BytesCopied,
            bindingPlan.In8BytesCopied);
    }
#endif
}

public readonly struct CheckedPortIoFunctionPointerInvocationResult
{
    private CheckedPortIoFunctionPointerInvocationResult(
        bool isInitialized,
        CheckedPortIoFunctionPointerInvocationStatus status,
        PortIoOperationKind operationKind,
        ushort port,
        byte value,
        bool wasInvoked)
    {
        IsInitialized = isInitialized;
        Status = status;
        OperationKind = operationKind;
        Port = port;
        Value = value;
        WasInvoked = wasInvoked;
    }

    public bool IsInitialized { get; }

    public CheckedPortIoFunctionPointerInvocationStatus Status { get; }

    public PortIoOperationKind OperationKind { get; }

    public ushort Port { get; }

    public byte Value { get; }

    public bool WasInvoked { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == CheckedPortIoFunctionPointerInvocationStatus.Success &&
        WasInvoked &&
        (OperationKind == PortIoOperationKind.Out8 || OperationKind == PortIoOperationKind.In8);

    internal static CheckedPortIoFunctionPointerInvocationResult Pass(
        PortIoOperationKind operationKind,
        ushort port,
        byte value)
    {
        return new CheckedPortIoFunctionPointerInvocationResult(
            true,
            CheckedPortIoFunctionPointerInvocationStatus.Success,
            operationKind,
            port,
            value,
            true);
    }

    internal static CheckedPortIoFunctionPointerInvocationResult Fail(
        CheckedPortIoFunctionPointerInvocationStatus status,
        PortIoOperationKind operationKind,
        ushort port,
        byte value)
    {
        return new CheckedPortIoFunctionPointerInvocationResult(
            true,
            status,
            operationKind,
            port,
            value,
            false);
    }
}

public readonly unsafe struct CheckedPortIoFunctionPointerBindingResult
{
    private readonly delegate* unmanaged<ushort, byte, void> _out8;
    private readonly delegate* unmanaged<ushort, byte> _in8;

    private CheckedPortIoFunctionPointerBindingResult(
        bool isInitialized,
        CheckedPortIoFunctionPointerBindingStatus status,
        ushort configuredBasePort,
        ushort configuredEndPort,
        nuint payloadBaseAddress,
        nuint payloadEndAddress,
        nuint out8StubAddress,
        nuint in8StubAddress,
        delegate* unmanaged<ushort, byte, void> out8,
        delegate* unmanaged<ushort, byte> in8)
    {
        IsInitialized = isInitialized;
        Status = status;
        ConfiguredBasePort = configuredBasePort;
        ConfiguredEndPort = configuredEndPort;
        PayloadBaseAddress = payloadBaseAddress;
        PayloadEndAddress = payloadEndAddress;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
        _out8 = out8;
        _in8 = in8;
    }

    public bool IsInitialized { get; }

    public CheckedPortIoFunctionPointerBindingStatus Status { get; }

    public ushort ConfiguredBasePort { get; }

    public ushort ConfiguredEndPort { get; }

    public nuint PayloadBaseAddress { get; }

    public nuint PayloadEndAddress { get; }

    public nuint Out8StubAddress { get; }

    public nuint In8StubAddress { get; }

    public bool CreatesManagedDelegate => false;

    public bool Succeeded =>
        IsInitialized &&
        Status == CheckedPortIoFunctionPointerBindingStatus.Ready &&
        ConfiguredBasePort == SerialPortConfiguration.Com1BasePort &&
        ConfiguredEndPort >= ConfiguredBasePort &&
        PayloadBaseAddress != 0 &&
        PayloadEndAddress > PayloadBaseAddress &&
        Out8StubAddress != 0 &&
        In8StubAddress != 0 &&
        _out8 != null &&
        _in8 != null &&
        !CreatesManagedDelegate;

    public CheckedPortIoFunctionPointerInvocationResult InvokeOut8(ushort port, byte value)
    {
        if (!Succeeded)
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(
                CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady,
                PortIoOperationKind.Out8,
                port,
                value);
        }

        if (!IsConfiguredPort(port))
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(
                CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange,
                PortIoOperationKind.Out8,
                port,
                value);
        }

        _out8(port, value);

        return CheckedPortIoFunctionPointerInvocationResult.Pass(
            PortIoOperationKind.Out8,
            port,
            value);
    }

    public CheckedPortIoFunctionPointerInvocationResult InvokeIn8(ushort port)
    {
        if (!Succeeded)
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(
                CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady,
                PortIoOperationKind.In8,
                port,
                0);
        }

        if (!IsConfiguredPort(port))
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(
                CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange,
                PortIoOperationKind.In8,
                port,
                0);
        }

        byte value = _in8(port);

        return CheckedPortIoFunctionPointerInvocationResult.Pass(
            PortIoOperationKind.In8,
            port,
            value);
    }

    internal static CheckedPortIoFunctionPointerBindingResult Ready(
        CheckedPortIoFunctionPointerBindingRequest request,
        ushort configuredEndPort,
        delegate* unmanaged<ushort, byte, void> out8,
        delegate* unmanaged<ushort, byte> in8)
    {
        return new CheckedPortIoFunctionPointerBindingResult(
            true,
            CheckedPortIoFunctionPointerBindingStatus.Ready,
            request.ConfiguredBasePort,
            configuredEndPort,
            request.PayloadBaseAddress,
            request.PayloadEndAddress,
            request.Out8StubAddress,
            request.In8StubAddress,
            out8,
            in8);
    }

    internal static CheckedPortIoFunctionPointerBindingResult Fail(
        CheckedPortIoFunctionPointerBindingStatus status,
        CheckedPortIoFunctionPointerBindingRequest request,
        ushort configuredEndPort)
    {
        return new CheckedPortIoFunctionPointerBindingResult(
            true,
            status,
            request.ConfiguredBasePort,
            configuredEndPort,
            request.PayloadBaseAddress,
            request.PayloadEndAddress,
            request.Out8StubAddress,
            request.In8StubAddress,
            null,
            null);
    }

    private bool IsConfiguredPort(ushort port)
    {
        return port >= ConfiguredBasePort && port <= ConfiguredEndPort;
    }
}

public static unsafe class InvokeIn8Out8CheckedUnmanagedFunctionPointers
{
#if !NOSTDLIB_EFI
    public static CheckedPortIoFunctionPointerBindingResult Bind(
        DiagnosticStubCallableBindingPlanResult bindingPlan)
    {
        if (!HasReadyPrerequisites(bindingPlan))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady,
                CheckedPortIoFunctionPointerBindingRequest.FromBindingPlan(bindingPlan),
                0);
        }

        return Bind(
            CheckedPortIoFunctionPointerBindingRequest.FromBindingPlan(bindingPlan));
    }
#endif

    public static CheckedPortIoFunctionPointerBindingResult Bind(
        CheckedPortIoFunctionPointerBindingRequest request)
    {
        if (!request.IsInitialized || !request.SourcePlanSucceeded)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady,
                request,
                0);
        }

        if (request.ConfiguredBasePort != SerialPortConfiguration.Com1BasePort)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.InvalidConfiguredBasePort,
                request,
                0);
        }

        uint configuredEndPort =
            (uint)request.ConfiguredBasePort + SerialPortConfiguration.MaximumRegisterOffset;
        if (configuredEndPort > ushort.MaxValue)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.ConfiguredPortRangeOverflow,
                request,
                0);
        }

        ushort checkedEndPort = (ushort)configuredEndPort;

        if (request.PayloadBaseAddress == 0 ||
            request.PayloadEndAddress <= request.PayloadBaseAddress ||
            request.BytesMaterialized == 0)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.InvalidPayloadRange,
                request,
                checkedEndPort);
        }

        if (request.Out8StubByteLength != PortIoStubPlan.Out8RequiredByteLength ||
            request.In8StubByteLength != PortIoStubPlan.In8RequiredByteLength ||
            request.Out8BytesCopied != PortIoStubPlan.Out8RequiredByteLength ||
            request.In8BytesCopied != PortIoStubPlan.In8RequiredByteLength)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.StubBytesNotReady,
                request,
                checkedEndPort);
        }

        if (!TryAdd(request.PayloadBaseAddress, request.Out8StubOffset, out nuint out8FromOffset) ||
            request.Out8StubAddress == 0 ||
            out8FromOffset != request.Out8StubAddress)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.Out8TargetMismatch,
                request,
                checkedEndPort);
        }

        if (!TryAdd(request.PayloadBaseAddress, request.In8StubOffset, out nuint in8FromOffset) ||
            request.In8StubAddress == 0 ||
            in8FromOffset != request.In8StubAddress)
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.In8TargetMismatch,
                request,
                checkedEndPort);
        }

        if (!RangeInsidePayload(
            request.Out8StubAddress,
            PortIoStubPlan.Out8RequiredByteLength,
            request.PayloadBaseAddress,
            request.PayloadEndAddress))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.Out8TargetOutsidePayload,
                request,
                checkedEndPort);
        }

        if (!RangeInsidePayload(
            request.In8StubAddress,
            PortIoStubPlan.In8RequiredByteLength,
            request.PayloadBaseAddress,
            request.PayloadEndAddress))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.In8TargetOutsidePayload,
                request,
                checkedEndPort);
        }

        if (RangesOverlap(
            request.Out8StubAddress,
            PortIoStubPlan.Out8RequiredByteLength,
            request.In8StubAddress,
            PortIoStubPlan.In8RequiredByteLength))
        {
            return CheckedPortIoFunctionPointerBindingResult.Fail(
                CheckedPortIoFunctionPointerBindingStatus.CallableRangesOverlap,
                request,
                checkedEndPort);
        }

        delegate* unmanaged<ushort, byte, void> out8 =
            (delegate* unmanaged<ushort, byte, void>)request.Out8StubAddress;
        delegate* unmanaged<ushort, byte> in8 =
            (delegate* unmanaged<ushort, byte>)request.In8StubAddress;

        return CheckedPortIoFunctionPointerBindingResult.Ready(
            request,
            checkedEndPort,
            out8,
            in8);
    }

#if !NOSTDLIB_EFI
    private static bool HasReadyPrerequisites(
        DiagnosticStubCallableBindingPlanResult bindingPlan)
    {
        return bindingPlan.IsInitialized &&
            bindingPlan.Status == DiagnosticStubCallableBindingPlanStatus.Ready &&
            bindingPlan.InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready &&
            bindingPlan.PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success &&
            bindingPlan.BackendContractStatus == Com1PortIoBackendContractStatus.Success &&
            bindingPlan.MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success;
    }
#endif

    private static bool RangeInsidePayload(
        nuint address,
        int byteLength,
        nuint payloadBaseAddress,
        nuint payloadEndAddress)
    {
        return TryAdd(address, (nuint)byteLength, out nuint endAddress) &&
            address >= payloadBaseAddress &&
            endAddress <= payloadEndAddress;
    }

    private static bool RangesOverlap(
        nuint firstAddress,
        int firstByteLength,
        nuint secondAddress,
        int secondByteLength)
    {
        if (!TryAdd(firstAddress, (nuint)firstByteLength, out nuint firstEnd) ||
            !TryAdd(secondAddress, (nuint)secondByteLength, out nuint secondEnd))
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
