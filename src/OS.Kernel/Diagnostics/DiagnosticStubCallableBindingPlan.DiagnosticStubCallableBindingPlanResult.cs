using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public readonly struct DiagnosticStubCallableBindingPlanResult
{
    private DiagnosticStubCallableBindingPlanResult(bool isInitialized, DiagnosticStubCallableBindingPlanStatus status, DiagnosticStubInvocationReadinessStatus invocationStatus, ExecutableStubPageTransitionPlanStatus pageTransitionStatus, Com1PortIoBackendContractStatus backendContractStatus, ExecutableStubBundleMaterializerStatus materializerStatus, ushort configuredBasePort, nuint payloadBaseAddress, nuint payloadEndAddress, nuint bytesMaterialized, nuint out8StubOffset, nuint in8StubOffset, nuint out8StubAddress, nuint in8StubAddress, int out8StubByteLength, int in8StubByteLength, int out8BytesCopied, int in8BytesCopied)
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
    public nuint PayloadByteLength => PayloadEndAddress >= PayloadBaseAddress ? PayloadEndAddress - PayloadBaseAddress : 0;
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
    public bool Succeeded => IsInitialized && Status == DiagnosticStubCallableBindingPlanStatus.Ready && InvocationStatus == DiagnosticStubInvocationReadinessStatus.Ready && PageTransitionStatus == ExecutableStubPageTransitionPlanStatus.Success && BackendContractStatus == Com1PortIoBackendContractStatus.Success && MaterializerStatus == ExecutableStubBundleMaterializerStatus.Success && ConfiguredBasePort == SerialPortConfiguration.Com1BasePort && PayloadBaseAddress != 0 && PayloadEndAddress > PayloadBaseAddress && BytesMaterialized != 0 && Out8StubByteLength == PortIoStubPlan.Out8RequiredByteLength && In8StubByteLength == PortIoStubPlan.In8RequiredByteLength && Out8BytesCopied == PortIoStubPlan.Out8RequiredByteLength && In8BytesCopied == PortIoStubPlan.In8RequiredByteLength && !CreatesManagedDelegate && !InvokesCallable && !PerformsPortIo && !WritesSerial;

    public static DiagnosticStubCallableBindingPlanResult Create(DiagnosticStubCallableBindingPlanStatus status, DiagnosticStubInvocationReadinessResult invocationReadiness, Com1PortIoBackendContractResult backendContract, ExecutableStubBundleMaterializerResult materializerResult)
    {
        return new DiagnosticStubCallableBindingPlanResult(true, status, invocationReadiness.Status, invocationReadiness.PageTransitionStatus, backendContract.Status, materializerResult.Status, backendContract.ConfiguredBasePort, invocationReadiness.PayloadBaseAddress, invocationReadiness.PayloadEndAddress, invocationReadiness.BytesMaterialized, backendContract.Out8StubOffset, backendContract.In8StubOffset, backendContract.Out8StubAddress, backendContract.In8StubAddress, PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength, materializerResult.Com1Out8BytesCopied, materializerResult.Com1In8BytesCopied);
    }
}
