namespace OS.Kernel.Runtime;
public readonly struct ExecutableStubPageTransitionPlanResult
{
    private ExecutableStubPageTransitionPlanResult(bool isInitialized, ExecutableStubPageTransitionPlanStatus status, ExecutableStubMemoryPlanStatus memoryPlanStatus, ExecutableStubBundleMaterializerStatus materializerStatus, nuint regionBaseAddress, nuint regionByteLength, nuint transitionBaseAddress, nuint transitionByteLength, nuint transitionEndAddress, nuint payloadBaseAddress, nuint payloadByteLength, nuint payloadEndAddress, nuint bytesMaterialized, ExecutableStubMemoryProtection writePhaseProtection, ExecutableStubMemoryProtection executePhaseProtection, ExecutableStubMemoryProtection finalProtection)
    {
        IsInitialized = isInitialized;
        Status = status;
        MemoryPlanStatus = memoryPlanStatus;
        MaterializerStatus = materializerStatus;
        RegionBaseAddress = regionBaseAddress;
        RegionByteLength = regionByteLength;
        TransitionBaseAddress = transitionBaseAddress;
        TransitionByteLength = transitionByteLength;
        TransitionEndAddress = transitionEndAddress;
        PayloadBaseAddress = payloadBaseAddress;
        PayloadByteLength = payloadByteLength;
        PayloadEndAddress = payloadEndAddress;
        BytesMaterialized = bytesMaterialized;
        WritePhaseProtection = writePhaseProtection;
        ExecutePhaseProtection = executePhaseProtection;
        FinalProtection = finalProtection;
    }

    public bool IsInitialized { get; }
    public ExecutableStubPageTransitionPlanStatus Status { get; }
    public ExecutableStubMemoryPlanStatus MemoryPlanStatus { get; }
    public ExecutableStubBundleMaterializerStatus MaterializerStatus { get; }
    public bool Succeeded => IsInitialized && Status == ExecutableStubPageTransitionPlanStatus.Success;
    public nuint RegionBaseAddress { get; }
    public nuint RegionByteLength { get; }
    public nuint TransitionBaseAddress { get; }
    public nuint TransitionByteLength { get; }
    public nuint TransitionEndAddress { get; }
    public nuint PayloadBaseAddress { get; }
    public nuint PayloadByteLength { get; }
    public nuint PayloadEndAddress { get; }
    public nuint BytesMaterialized { get; }
    public ExecutableStubMemoryProtection WritePhaseProtection { get; }
    public ExecutableStubMemoryProtection ExecutePhaseProtection { get; }
    public ExecutableStubMemoryProtection FinalProtection { get; }

    public static ExecutableStubPageTransitionPlanResult Success(ExecutableStubMemoryPlanResult memoryPlan, ExecutableStubBundleMaterializerResult materializerResult, nuint regionBaseAddress, nuint regionByteLength, nuint transitionEndAddress, nuint payloadBaseAddress, nuint payloadEndAddress)
    {
        return new ExecutableStubPageTransitionPlanResult(true, ExecutableStubPageTransitionPlanStatus.Success, memoryPlan.Status, materializerResult.Status, regionBaseAddress, regionByteLength, regionBaseAddress, memoryPlan.RequiredByteLength, transitionEndAddress, payloadBaseAddress, memoryPlan.RequestedPayloadLength, payloadEndAddress, materializerResult.BytesCopied, memoryPlan.WritePhaseProtection, memoryPlan.ExecutePhaseProtection, memoryPlan.FinalProtection);
    }

    public static ExecutableStubPageTransitionPlanResult Fail(ExecutableStubPageTransitionPlanStatus status, ExecutableStubMemoryPlanStatus memoryPlanStatus, ExecutableStubBundleMaterializerStatus materializerStatus)
    {
        return new ExecutableStubPageTransitionPlanResult(true, status, memoryPlanStatus, materializerStatus, 0, 0, 0, 0, 0, 0, 0, 0, 0, ExecutableStubMemoryProtection.None, ExecutableStubMemoryProtection.None, ExecutableStubMemoryProtection.None);
    }
}
