using OS.Kernel.Memory;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
/// <summary>
/// A snapshot, never a second mutable allocator authority. Success from retained
/// validation reports ownership even when ContentsAreZeroNow is false.
/// </summary>

public readonly struct ManagedHeapPageRangeResult
{
    internal ManagedHeapPageRangeResult(ManagedHeapPageRangeStatus status, ManagedHeapPageRangeLifecycle lifecycle = default, ulong physicalStart = 0, ulong physicalEnd = 0, ulong mappedStart = 0, BootPageAllocatorState before = default, BootPageAllocatorState current = default, ulong readBytes = 0, bool contentsZero = false, BootPageAllocatorMutationStatus allocationStatus = default)
    {
        Status = status;
        Lifecycle = lifecycle;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEnd;
        MappedStart = mappedStart;
        InitialAllocatorState = before;
        CurrentAllocatorState = current;
        ReadBytes = readBytes;
        ContentsAreZeroNow = contentsZero;
        AllocationStatus = allocationStatus;
    }

    public ManagedHeapPageRangeStatus Status
    {
        get;
    }
    public ManagedHeapPageRangeLifecycle Lifecycle
    {
        get;
    }
    public bool Succeeded => Status == ManagedHeapPageRangeStatus.Success;
    public bool IsReady => Succeeded && Lifecycle == ManagedHeapPageRangeLifecycle.Ready;
    public ulong PhysicalStart
    {
        get;
    }
    public ulong PhysicalEndExclusive
    {
        get;
    }
    public ulong MappedStart
    {
        get;
    }
    public BootPageAllocatorState InitialAllocatorState
    {
        get;
    }
    public BootPageAllocatorState CurrentAllocatorState
    {
        get;
    }
    public ulong ReadBytes
    {
        get;
    }
    public bool ContentsAreZeroNow
    {
        get;
    }
    // Relevant only when Status is AllocationRejected.
    public BootPageAllocatorMutationStatus AllocationStatus
    {
        get;
    }
}
