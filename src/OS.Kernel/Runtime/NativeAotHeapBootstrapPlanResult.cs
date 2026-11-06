using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotHeapBootstrapPlanResult
{
    private NativeAotHeapBootstrapPlanResult(bool isInitialized, NativeAotHeapBootstrapPlanStatus status, ulong pageSize, ulong allocatorFreePages, ulong allocatorFreeBytes, ulong expectedAllocatorFreeBytes, ulong requestedHeapPages, ulong requestedHeapBytes, ulong remainingFreePages, ulong remainingFreeBytes, ulong objectAlignment)
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

    public bool IsInitialized
    {
        get;
    }
    public NativeAotHeapBootstrapPlanStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAotHeapBootstrapPlanStatus.Success;
    public ulong PageSize
    {
        get;
    }
    public ulong AllocatorFreePages
    {
        get;
    }
    public ulong AllocatorFreeBytes
    {
        get;
    }
    public ulong ExpectedAllocatorFreeBytes
    {
        get;
    }
    public ulong RequestedHeapPages
    {
        get;
    }
    public ulong RequestedHeapBytes
    {
        get;
    }
    public ulong RemainingFreePages
    {
        get;
    }
    public ulong RemainingFreeBytes
    {
        get;
    }
    public ulong ObjectAlignment
    {
        get;
    }
    public bool RequiresPageAllocatorMutation => Succeeded;
    public bool RequiresGcRootScanning => Succeeded;
    public bool RequiresWriteBarriers => Succeeded;
    public bool SatisfiesObjectAllocationHelpers => false;

    internal static NativeAotHeapBootstrapPlanResult Success(BootPageAllocatorState allocatorState, ulong expectedAllocatorFreeBytes, ulong requestedHeapPages, ulong requestedHeapBytes, ulong objectAlignment)
    {
        ulong remainingFreePages = allocatorState.FreePageCount - requestedHeapPages;
        ulong remainingFreeBytes = expectedAllocatorFreeBytes - requestedHeapBytes;
        return new NativeAotHeapBootstrapPlanResult(isInitialized: true, NativeAotHeapBootstrapPlanStatus.Success, allocatorState.PageSize, allocatorState.FreePageCount, allocatorState.FreeByteCount, expectedAllocatorFreeBytes, requestedHeapPages, requestedHeapBytes, remainingFreePages, remainingFreeBytes, objectAlignment);
    }

    internal static NativeAotHeapBootstrapPlanResult Fail(NativeAotHeapBootstrapPlanStatus status, BootPageAllocatorState allocatorState, ulong expectedAllocatorFreeBytes, ulong requestedHeapPages, ulong requestedHeapBytes, ulong objectAlignment)
    {
        return new NativeAotHeapBootstrapPlanResult(isInitialized: true, status, allocatorState.PageSize, allocatorState.FreePageCount, allocatorState.FreeByteCount, expectedAllocatorFreeBytes, requestedHeapPages, requestedHeapBytes, remainingFreePages: 0, remainingFreeBytes: 0, objectAlignment);
    }
}
