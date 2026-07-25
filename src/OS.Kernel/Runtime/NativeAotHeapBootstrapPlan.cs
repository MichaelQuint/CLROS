using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public enum NativeAotHeapBootstrapPlanStatus
{
    Success = 0,
    AllocatorStateNotInitialized = 1,
    InvalidPageSize = 2,
    NoFreePages = 3,
    AllocatorFreeByteCountOverflow = 4,
    AllocatorFreeByteCountMismatch = 5,
    RequestedHeapPagesZero = 6,
    RequestedHeapPagesExceedFreePages = 7,
    InvalidObjectAlignment = 8,
    ObjectAlignmentTooSmall = 9,
    ObjectAlignmentExceedsPageSize = 10
}

public readonly struct NativeAotHeapBootstrapPlanResult
{
    private NativeAotHeapBootstrapPlanResult(
        bool isInitialized,
        NativeAotHeapBootstrapPlanStatus status,
        ulong pageSize,
        ulong allocatorFreePages,
        ulong allocatorFreeBytes,
        ulong expectedAllocatorFreeBytes,
        ulong requestedHeapPages,
        ulong requestedHeapBytes,
        ulong remainingFreePages,
        ulong remainingFreeBytes,
        ulong objectAlignment)
    {
        IsInitialized = isInitialized;
        Status = status;
        PageSize = pageSize;
        AllocatorFreePages = allocatorFreePages;
        AllocatorFreeBytes = allocatorFreeBytes;
        ExpectedAllocatorFreeBytes = expectedAllocatorFreeBytes;
        RequestedHeapPages = requestedHeapPages;
        RequestedHeapBytes = requestedHeapBytes;
        RemainingFreePages = remainingFreePages;
        RemainingFreeBytes = remainingFreeBytes;
        ObjectAlignment = objectAlignment;
    }

    public bool IsInitialized { get; }

    public NativeAotHeapBootstrapPlanStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == NativeAotHeapBootstrapPlanStatus.Success;

    public ulong PageSize { get; }

    public ulong AllocatorFreePages { get; }

    public ulong AllocatorFreeBytes { get; }

    public ulong ExpectedAllocatorFreeBytes { get; }

    public ulong RequestedHeapPages { get; }

    public ulong RequestedHeapBytes { get; }

    public ulong RemainingFreePages { get; }

    public ulong RemainingFreeBytes { get; }

    public ulong ObjectAlignment { get; }

    public bool RequiresPageAllocatorMutation => Succeeded;

    public bool RequiresGcRootScanning => Succeeded;

    public bool RequiresWriteBarriers => Succeeded;

    public bool SatisfiesObjectAllocationHelpers => false;

    internal static NativeAotHeapBootstrapPlanResult Success(
        BootPageAllocatorState allocatorState,
        ulong expectedAllocatorFreeBytes,
        ulong requestedHeapPages,
        ulong requestedHeapBytes,
        ulong objectAlignment)
    {
        ulong remainingFreePages = allocatorState.FreePageCount - requestedHeapPages;
        ulong remainingFreeBytes = expectedAllocatorFreeBytes - requestedHeapBytes;

        return new NativeAotHeapBootstrapPlanResult(
            isInitialized: true,
            NativeAotHeapBootstrapPlanStatus.Success,
            allocatorState.PageSize,
            allocatorState.FreePageCount,
            allocatorState.FreeByteCount,
            expectedAllocatorFreeBytes,
            requestedHeapPages,
            requestedHeapBytes,
            remainingFreePages,
            remainingFreeBytes,
            objectAlignment);
    }

    internal static NativeAotHeapBootstrapPlanResult Fail(
        NativeAotHeapBootstrapPlanStatus status,
        BootPageAllocatorState allocatorState,
        ulong expectedAllocatorFreeBytes,
        ulong requestedHeapPages,
        ulong requestedHeapBytes,
        ulong objectAlignment)
    {
        return new NativeAotHeapBootstrapPlanResult(
            isInitialized: true,
            status,
            allocatorState.PageSize,
            allocatorState.FreePageCount,
            allocatorState.FreeByteCount,
            expectedAllocatorFreeBytes,
            requestedHeapPages,
            requestedHeapBytes,
            remainingFreePages: 0,
            remainingFreeBytes: 0,
            objectAlignment);
    }
}

public static class NativeAotHeapBootstrapPlanner
{
    public const ulong MinimumObjectAlignment = 8;

    public static NativeAotHeapBootstrapPlanResult Plan(
        BootPageAllocatorState allocatorState,
        ulong requestedHeapPages,
        ulong objectAlignment)
    {
        if (!allocatorState.IsInitialized)
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.AllocatorStateNotInitialized,
                allocatorState,
                expectedAllocatorFreeBytes: 0,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        if (!IsValidPageSize(allocatorState.PageSize))
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.InvalidPageSize,
                allocatorState,
                expectedAllocatorFreeBytes: 0,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        if (allocatorState.FreePageCount == 0)
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.NoFreePages,
                allocatorState,
                expectedAllocatorFreeBytes: 0,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        if (!TryMultiply(allocatorState.FreePageCount, allocatorState.PageSize, out ulong expectedFreeBytes))
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.AllocatorFreeByteCountOverflow,
                allocatorState,
                expectedAllocatorFreeBytes: 0,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        if (allocatorState.FreeByteCount != expectedFreeBytes)
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.AllocatorFreeByteCountMismatch,
                allocatorState,
                expectedFreeBytes,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        if (requestedHeapPages == 0)
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.RequestedHeapPagesZero,
                allocatorState,
                expectedFreeBytes,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        if (requestedHeapPages > allocatorState.FreePageCount)
        {
            return Fail(
                NativeAotHeapBootstrapPlanStatus.RequestedHeapPagesExceedFreePages,
                allocatorState,
                expectedFreeBytes,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        NativeAotHeapBootstrapPlanStatus alignmentStatus = ValidateObjectAlignment(
            objectAlignment,
            allocatorState.PageSize);
        if (alignmentStatus != NativeAotHeapBootstrapPlanStatus.Success)
        {
            return Fail(
                alignmentStatus,
                allocatorState,
                expectedFreeBytes,
                requestedHeapPages,
                requestedHeapBytes: 0,
                objectAlignment);
        }

        ulong requestedHeapBytes = requestedHeapPages * allocatorState.PageSize;
        return NativeAotHeapBootstrapPlanResult.Success(
            allocatorState,
            expectedFreeBytes,
            requestedHeapPages,
            requestedHeapBytes,
            objectAlignment);
    }

    private static NativeAotHeapBootstrapPlanResult Fail(
        NativeAotHeapBootstrapPlanStatus status,
        BootPageAllocatorState allocatorState,
        ulong expectedAllocatorFreeBytes,
        ulong requestedHeapPages,
        ulong requestedHeapBytes,
        ulong objectAlignment)
    {
        return NativeAotHeapBootstrapPlanResult.Fail(
            status,
            allocatorState,
            expectedAllocatorFreeBytes,
            requestedHeapPages,
            requestedHeapBytes,
            objectAlignment);
    }

    private static NativeAotHeapBootstrapPlanStatus ValidateObjectAlignment(
        ulong objectAlignment,
        ulong pageSize)
    {
        if (!IsPowerOfTwo(objectAlignment))
        {
            return NativeAotHeapBootstrapPlanStatus.InvalidObjectAlignment;
        }

        if (objectAlignment < MinimumObjectAlignment)
        {
            return NativeAotHeapBootstrapPlanStatus.ObjectAlignmentTooSmall;
        }

        return objectAlignment <= pageSize
            ? NativeAotHeapBootstrapPlanStatus.Success
            : NativeAotHeapBootstrapPlanStatus.ObjectAlignmentExceedsPageSize;
    }

    private static bool IsValidPageSize(ulong pageSize)
    {
        return pageSize != 0 && IsPowerOfTwo(pageSize);
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryMultiply(ulong left, ulong right, out ulong value)
    {
        if (left != 0 && right > ulong.MaxValue / left)
        {
            value = 0;
            return false;
        }

        value = left * right;
        return true;
    }
}
