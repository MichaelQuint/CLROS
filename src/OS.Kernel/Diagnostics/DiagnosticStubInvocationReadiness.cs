using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum DiagnosticStubInvocationReadinessStatus
{
    Ready = 0,
    PageTransitionNotReady = 1,
    BackendContractNotReady = 2,
    Out8AddressOutsideRegion = 3,
    In8AddressOutsideRegion = 4
}

public readonly struct DiagnosticStubInvocationReadinessResult
{
    private DiagnosticStubInvocationReadinessResult(
        bool isInitialized,
        DiagnosticStubInvocationReadinessStatus status,
        ExecutableStubPageTransitionPlanStatus pageTransitionStatus,
        Com1PortIoBackendContractStatus backendContractStatus,
        nuint payloadBaseAddress,
        nuint payloadEndAddress,
        nuint bytesMaterialized,
        nuint out8StubAddress,
        nuint in8StubAddress)
    {
        IsInitialized = isInitialized;
        Status = status;
        PageTransitionStatus = pageTransitionStatus;
        BackendContractStatus = backendContractStatus;
        PayloadBaseAddress = payloadBaseAddress;
        PayloadEndAddress = payloadEndAddress;
        BytesMaterialized = bytesMaterialized;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
    }

    public bool IsInitialized { get; }

    public DiagnosticStubInvocationReadinessStatus Status { get; }

    public ExecutableStubPageTransitionPlanStatus PageTransitionStatus { get; }

    public Com1PortIoBackendContractStatus BackendContractStatus { get; }

    public nuint PayloadBaseAddress { get; }

    public nuint PayloadEndAddress { get; }

    public nuint BytesMaterialized { get; }

    public nuint Out8StubAddress { get; }

    public nuint In8StubAddress { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == DiagnosticStubInvocationReadinessStatus.Ready &&
        PayloadBaseAddress != 0 &&
        PayloadEndAddress > PayloadBaseAddress &&
        BytesMaterialized != 0 &&
        IsInsidePayload(Out8StubAddress, PayloadBaseAddress, PayloadEndAddress) &&
        IsInsidePayload(In8StubAddress, PayloadBaseAddress, PayloadEndAddress);

    public static DiagnosticStubInvocationReadinessResult Pass(
        ExecutableStubPageTransitionPlanResult pageTransition,
        Com1PortIoBackendContractResult backendContract)
    {
        return new DiagnosticStubInvocationReadinessResult(
            true,
            DiagnosticStubInvocationReadinessStatus.Ready,
            pageTransition.Status,
            backendContract.Status,
            pageTransition.PayloadBaseAddress,
            pageTransition.PayloadEndAddress,
            pageTransition.BytesMaterialized,
            backendContract.Out8StubAddress,
            backendContract.In8StubAddress);
    }

    public static DiagnosticStubInvocationReadinessResult Fail(
        DiagnosticStubInvocationReadinessStatus status,
        ExecutableStubPageTransitionPlanResult pageTransition,
        Com1PortIoBackendContractResult backendContract)
    {
        return new DiagnosticStubInvocationReadinessResult(
            true,
            status,
            pageTransition.Status,
            backendContract.Status,
            pageTransition.PayloadBaseAddress,
            pageTransition.PayloadEndAddress,
            pageTransition.BytesMaterialized,
            backendContract.Out8StubAddress,
            backendContract.In8StubAddress);
    }

    public static bool IsInsidePayload(
        nuint address,
        nuint payloadBaseAddress,
        nuint payloadEndAddress)
    {
        return address >= payloadBaseAddress && address < payloadEndAddress;
    }
}

public static class DiagnosticStubInvocationReadiness
{
    public static DiagnosticStubInvocationReadinessResult RequireReady(
        ExecutableStubPageTransitionPlanResult pageTransition,
        Com1PortIoBackendContractResult backendContract)
    {
        if (!pageTransition.Succeeded || pageTransition.BytesMaterialized == 0)
        {
            return DiagnosticStubInvocationReadinessResult.Fail(
                DiagnosticStubInvocationReadinessStatus.PageTransitionNotReady,
                pageTransition,
                backendContract);
        }

        if (!backendContract.Succeeded)
        {
            return DiagnosticStubInvocationReadinessResult.Fail(
                DiagnosticStubInvocationReadinessStatus.BackendContractNotReady,
                pageTransition,
                backendContract);
        }

        if (!DiagnosticStubInvocationReadinessResult.IsInsidePayload(
            backendContract.Out8StubAddress,
            pageTransition.PayloadBaseAddress,
            pageTransition.PayloadEndAddress))
        {
            return DiagnosticStubInvocationReadinessResult.Fail(
                DiagnosticStubInvocationReadinessStatus.Out8AddressOutsideRegion,
                pageTransition,
                backendContract);
        }

        if (!DiagnosticStubInvocationReadinessResult.IsInsidePayload(
            backendContract.In8StubAddress,
            pageTransition.PayloadBaseAddress,
            pageTransition.PayloadEndAddress))
        {
            return DiagnosticStubInvocationReadinessResult.Fail(
                DiagnosticStubInvocationReadinessStatus.In8AddressOutsideRegion,
                pageTransition,
                backendContract);
        }

        return DiagnosticStubInvocationReadinessResult.Pass(pageTransition, backendContract);
    }
}
