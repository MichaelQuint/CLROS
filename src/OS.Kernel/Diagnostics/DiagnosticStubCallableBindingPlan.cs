using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum DiagnosticStubCallableBindingPlanStatus
{
    Ready = 0,
    InvocationReadinessNotReady = 1,
    BackendContractNotReady = 2,
    BackendContractMismatch = 3,
    MaterializerNotReady = 4,
    Out8BytesNotMaterialized = 5,
    In8BytesNotMaterialized = 6,
    Out8CallableOutsidePayload = 7,
    In8CallableOutsidePayload = 8,
    CallableRangesOverlap = 9,
    InvalidCom1BasePort = 10
}

public readonly struct DiagnosticStubCallableBindingPlanResult
{
    private DiagnosticStubCallableBindingPlanResult(
        bool isInitialized,
        DiagnosticStubCallableBindingPlanStatus status,
        DiagnosticStubInvocationReadinessStatus invocationStatus,
        ExecutableStubPageTransitionPlanStatus pageTransitionStatus,
        Com1PortIoBackendContractStatus backendContractStatus,
        ExecutableStubBundleMaterializerStatus materializerStatus,
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
        Status = status;
        InvocationStatus = invocationStatus;
        PageTransitionStatus = pageTransitionStatus;
        BackendContractStatus = backendContractStatus;
        MaterializerStatus = materializerStatus;
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

    public DiagnosticStubCallableBindingPlanStatus Status { get; }

    public DiagnosticStubInvocationReadinessStatus InvocationStatus { get; }

    public ExecutableStubPageTransitionPlanStatus PageTransitionStatus { get; }

    public Com1PortIoBackendContractStatus BackendContractStatus { get; }

    public ExecutableStubBundleMaterializerStatus MaterializerStatus { get; }

    public ushort ConfiguredBasePort { get; }

    public nuint PayloadBaseAddress { get; }

    public nuint PayloadEndAddress { get; }

    public nuint BytesMaterialized { get; }

    public nuint PayloadByteLength =>
        PayloadEndAddress >= PayloadBaseAddress
            ? PayloadEndAddress - PayloadBaseAddress
            : 0;

    public nuint Out8StubOffset { get; }

    public nuint In8StubOffset { get; }

    public nuint Out8StubAddress { get; }

    public nuint In8StubAddress { get; }

    public int Out8StubByteLength { get; }

    public int In8StubByteLength { get; }

    public int Out8BytesCopied { get; }

    public int In8BytesCopied { get; }

    public bool CreatesManagedDelegate => false;

    public bool InvokesCallable => false;

    public bool PerformsPortIo => false;

    public bool WritesSerial => false;

    public bool Succeeded =>
        IsInitialized &&
        Status == DiagnosticStubCallableBindingPlanStatus.Ready &&
        InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready &&
        PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success &&
        BackendContractStatus == Com1PortIoBackendContractStatus.Success &&
        MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success &&
        ConfiguredBasePort == SerialPortConfiguration.Com1BasePort &&
        PayloadBaseAddress != 0 &&
        PayloadEndAddress > PayloadBaseAddress &&
        BytesMaterialized != 0 &&
        Out8StubByteLength == PortIoStubPlan.Out8RequiredByteLength &&
        In8StubByteLength == PortIoStubPlan.In8RequiredByteLength &&
        Out8BytesCopied == PortIoStubPlan.Out8RequiredByteLength &&
        In8BytesCopied == PortIoStubPlan.In8RequiredByteLength &&
        !CreatesManagedDelegate &&
        !InvokesCallable &&
        !PerformsPortIo &&
        !WritesSerial;

    public static DiagnosticStubCallableBindingPlanResult Create(
        DiagnosticStubCallableBindingPlanStatus status,
        DiagnosticStubInvocationReadinessResult invocationReadiness,
        Com1PortIoBackendContractResult backendContract,
        ExecutableStubBundleMaterializerResult materializerResult)
    {
        return new DiagnosticStubCallableBindingPlanResult(
            true,
            status,
            invocationReadiness.Status,
            invocationReadiness.PageTransitionStatus,
            backendContract.Status,
            materializerResult.Status,
            backendContract.ConfiguredBasePort,
            invocationReadiness.PayloadBaseAddress,
            invocationReadiness.PayloadEndAddress,
            invocationReadiness.BytesMaterialized,
            backendContract.Out8StubOffset,
            backendContract.In8StubOffset,
            backendContract.Out8StubAddress,
            backendContract.In8StubAddress,
            PortIoStubPlan.Out8RequiredByteLength,
            PortIoStubPlan.In8RequiredByteLength,
            materializerResult.Com1Out8BytesCopied,
            materializerResult.Com1In8BytesCopied);
    }
}

public static class DiagnosticStubCallableBindingPlan
{
    public static DiagnosticStubCallableBindingPlanResult Create(
        DiagnosticStubInvocationReadinessResult invocationReadiness,
        Com1PortIoBackendContractResult backendContract,
        ExecutableStubBundleMaterializerResult materializerResult)
    {
        if (!invocationReadiness.Succeeded)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.InvocationReadinessNotReady,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (!backendContract.Succeeded)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.BackendContractNotReady,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (backendContract.ConfiguredBasePort != SerialPortConfiguration.Com1BasePort)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.InvalidCom1BasePort,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (!BackendContractMatchesInvocation(invocationReadiness, backendContract))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.BackendContractMismatch,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (!materializerResult.Succeeded)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.MaterializerNotReady,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (materializerResult.Com1Out8BytesCopied != PortIoStubPlan.Out8RequiredByteLength)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.Out8BytesNotMaterialized,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (materializerResult.Com1In8BytesCopied != PortIoStubPlan.In8RequiredByteLength)
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.In8BytesNotMaterialized,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (!CallableRangeInsidePayload(
            backendContract.Out8StubAddress,
            PortIoStubPlan.Out8RequiredByteLength,
            invocationReadiness.PayloadBaseAddress,
            invocationReadiness.PayloadEndAddress))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.Out8CallableOutsidePayload,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (!CallableRangeInsidePayload(
            backendContract.In8StubAddress,
            PortIoStubPlan.In8RequiredByteLength,
            invocationReadiness.PayloadBaseAddress,
            invocationReadiness.PayloadEndAddress))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.In8CallableOutsidePayload,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        if (RangesOverlap(
            backendContract.Out8StubAddress,
            PortIoStubPlan.Out8RequiredByteLength,
            backendContract.In8StubAddress,
            PortIoStubPlan.In8RequiredByteLength))
        {
            return DiagnosticStubCallableBindingPlanResult.Create(
                DiagnosticStubCallableBindingPlanStatus.CallableRangesOverlap,
                invocationReadiness,
                backendContract,
                materializerResult);
        }

        return DiagnosticStubCallableBindingPlanResult.Create(
            DiagnosticStubCallableBindingPlanStatus.Ready,
            invocationReadiness,
            backendContract,
            materializerResult);
    }

    private static bool BackendContractMatchesInvocation(
        DiagnosticStubInvocationReadinessResult invocationReadiness,
        Com1PortIoBackendContractResult backendContract)
    {
        return invocationReadiness.BackendContractStatus == backendContract.Status &&
            invocationReadiness.Out8StubAddress == backendContract.Out8StubAddress &&
            invocationReadiness.In8StubAddress == backendContract.In8StubAddress &&
            TryAdd(invocationReadiness.PayloadBaseAddress, backendContract.Out8StubOffset, out nuint out8FromOffset) &&
            TryAdd(invocationReadiness.PayloadBaseAddress, backendContract.In8StubOffset, out nuint in8FromOffset) &&
            out8FromOffset == backendContract.Out8StubAddress &&
            in8FromOffset == backendContract.In8StubAddress;
    }

    private static bool CallableRangeInsidePayload(
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
