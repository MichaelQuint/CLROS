namespace OS.Kernel.Runtime;

public enum ExecutableStubPageTransitionPlanStatus
{
    Success = 0,
    InvalidMemoryPlan = 1,
    UnmaterializedPayload = 2,
    UnsafeProtectionState = 3,
    InvalidRange = 4,
    RangeOverflow = 5
}

public readonly struct ExecutableStubPageTransitionPlanResult
{
    private ExecutableStubPageTransitionPlanResult(
        bool isInitialized,
        ExecutableStubPageTransitionPlanStatus status,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        ExecutableStubBundleMaterializerStatus materializerStatus,
        nuint regionBaseAddress,
        nuint regionByteLength,
        nuint transitionBaseAddress,
        nuint transitionByteLength,
        nuint transitionEndAddress,
        nuint payloadBaseAddress,
        nuint payloadByteLength,
        nuint payloadEndAddress,
        nuint bytesMaterialized,
        ExecutableStubMemoryProtection writePhaseProtection,
        ExecutableStubMemoryProtection executePhaseProtection,
        ExecutableStubMemoryProtection finalProtection)
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

    public static ExecutableStubPageTransitionPlanResult Success(
        ExecutableStubMemoryPlanResult memoryPlan,
        ExecutableStubBundleMaterializerResult materializerResult,
        nuint regionBaseAddress,
        nuint regionByteLength,
        nuint transitionEndAddress,
        nuint payloadBaseAddress,
        nuint payloadEndAddress)
    {
        return new ExecutableStubPageTransitionPlanResult(
            true,
            ExecutableStubPageTransitionPlanStatus.Success,
            memoryPlan.Status,
            materializerResult.Status,
            regionBaseAddress,
            regionByteLength,
            regionBaseAddress,
            memoryPlan.RequiredByteLength,
            transitionEndAddress,
            payloadBaseAddress,
            memoryPlan.RequestedPayloadLength,
            payloadEndAddress,
            materializerResult.BytesCopied,
            memoryPlan.WritePhaseProtection,
            memoryPlan.ExecutePhaseProtection,
            memoryPlan.FinalProtection);
    }

    public static ExecutableStubPageTransitionPlanResult Fail(
        ExecutableStubPageTransitionPlanStatus status,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        ExecutableStubBundleMaterializerStatus materializerStatus)
    {
        return new ExecutableStubPageTransitionPlanResult(
            true,
            status,
            memoryPlanStatus,
            materializerStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            ExecutableStubMemoryProtection.None,
            ExecutableStubMemoryProtection.None,
            ExecutableStubMemoryProtection.None);
    }
}

public static class ExecutableStubPageTransitionPlan
{
    public static ExecutableStubPageTransitionPlanResult Create(
        ExecutableStubMemoryPlanResult memoryPlan,
        ExecutableStubBundleMaterializerResult materializerResult,
        nuint regionBaseAddress,
        nuint regionByteLength)
    {
        if (!IsValidMemoryPlan(memoryPlan))
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.InvalidMemoryPlan,
                memoryPlan.Status,
                materializerResult.Status);
        }

        if (!materializerResult.Succeeded || materializerResult.BytesCopied == 0)
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.UnmaterializedPayload,
                memoryPlan.Status,
                materializerResult.Status);
        }

        if (HasUnsafeProtectionState(memoryPlan))
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.UnsafeProtectionState,
                memoryPlan.Status,
                materializerResult.Status);
        }

        if (!IsValidRegion(memoryPlan, regionBaseAddress, regionByteLength))
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.InvalidRange,
                memoryPlan.Status,
                materializerResult.Status);
        }

        nuint regionEndAddress = regionBaseAddress + regionByteLength;
        if (regionEndAddress < regionBaseAddress)
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.RangeOverflow,
                memoryPlan.Status,
                materializerResult.Status);
        }

        nuint transitionEndAddress = regionBaseAddress + memoryPlan.RequiredByteLength;
        if (transitionEndAddress < regionBaseAddress || transitionEndAddress > regionEndAddress)
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.RangeOverflow,
                memoryPlan.Status,
                materializerResult.Status);
        }

        nuint payloadBaseAddress = regionBaseAddress + memoryPlan.PayloadOffset;
        if (payloadBaseAddress < regionBaseAddress)
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.RangeOverflow,
                memoryPlan.Status,
                materializerResult.Status);
        }

        nuint payloadEndAddress = payloadBaseAddress + memoryPlan.RequestedPayloadLength;
        if (payloadEndAddress < payloadBaseAddress || payloadEndAddress > transitionEndAddress)
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.RangeOverflow,
                memoryPlan.Status,
                materializerResult.Status);
        }

        if (materializerResult.BytesCopied > memoryPlan.RequestedPayloadLength)
        {
            return ExecutableStubPageTransitionPlanResult.Fail(
                ExecutableStubPageTransitionPlanStatus.InvalidRange,
                memoryPlan.Status,
                materializerResult.Status);
        }

        return ExecutableStubPageTransitionPlanResult.Success(
            memoryPlan,
            materializerResult,
            regionBaseAddress,
            regionByteLength,
            transitionEndAddress,
            payloadBaseAddress,
            payloadEndAddress);
    }

    private static bool IsValidMemoryPlan(ExecutableStubMemoryPlanResult memoryPlan)
    {
        return memoryPlan.Succeeded
            && memoryPlan.RequestedPayloadLength != 0
            && memoryPlan.RequiredByteLength != 0
            && memoryPlan.PageSize >= ExecutableStubMemoryPlan.MinimumPageSize
            && IsPowerOfTwo(memoryPlan.PageSize)
            && memoryPlan.Alignment != 0
            && IsPowerOfTwo(memoryPlan.Alignment)
            && memoryPlan.Alignment <= memoryPlan.PageSize
            && memoryPlan.PayloadOffset <= memoryPlan.RequiredByteLength
            && IsAligned(memoryPlan.PayloadOffset, memoryPlan.Alignment)
            && IsAligned(memoryPlan.RequiredByteLength, memoryPlan.PageSize);
    }

    private static bool HasUnsafeProtectionState(ExecutableStubMemoryPlanResult memoryPlan)
    {
        return memoryPlan.WritePhaseProtection == ExecutableStubMemoryProtection.None
            || memoryPlan.ExecutePhaseProtection == ExecutableStubMemoryProtection.None
            || memoryPlan.FinalProtection == ExecutableStubMemoryProtection.None
            || ExecutableStubMemoryPlan.IsWritableExecutable(memoryPlan.WritePhaseProtection)
            || ExecutableStubMemoryPlan.IsWritableExecutable(memoryPlan.ExecutePhaseProtection)
            || ExecutableStubMemoryPlan.IsWritableExecutable(memoryPlan.FinalProtection)
            || (memoryPlan.WritePhaseProtection & ExecutableStubMemoryProtection.Write) == 0
            || (memoryPlan.WritePhaseProtection & ExecutableStubMemoryProtection.Execute) != 0
            || (memoryPlan.ExecutePhaseProtection & ExecutableStubMemoryProtection.Execute) == 0
            || (memoryPlan.ExecutePhaseProtection & ExecutableStubMemoryProtection.Write) != 0
            || (memoryPlan.FinalProtection & ExecutableStubMemoryProtection.Execute) == 0
            || (memoryPlan.FinalProtection & ExecutableStubMemoryProtection.Write) != 0;
    }

    private static bool IsValidRegion(
        ExecutableStubMemoryPlanResult memoryPlan,
        nuint regionBaseAddress,
        nuint regionByteLength)
    {
        return regionBaseAddress != 0
            && regionByteLength != 0
            && IsAligned(regionBaseAddress, memoryPlan.PageSize)
            && IsAligned(regionByteLength, memoryPlan.PageSize)
            && regionByteLength >= memoryPlan.RequiredByteLength;
    }

    private static bool IsAligned(nuint value, uint alignment)
    {
        nuint mask = (nuint)alignment - 1;
        return (value & mask) == 0;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }
}
