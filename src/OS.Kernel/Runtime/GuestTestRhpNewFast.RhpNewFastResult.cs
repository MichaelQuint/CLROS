using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>Scalar observations only; ObjectReference is never a host CLR object.</summary>

public readonly struct RhpNewFastResult
{
    internal RhpNewFastResult(RhpNewFastStatus status, RhpNewFastOperation operation, ManagedHeapPageRangeStatus heapStatus = default, ulong mappedStart = 0, ulong mappedEnd = 0, ulong physicalStart = 0, ulong physicalEnd = 0, ulong objectReference = 0, ulong methodTableToken = 0, ulong bytesConsumed = 0, ulong usedBefore = 0, ulong usedAfter = 0, ulong allocationCount = 0)
    {
        Status = status;
        Operation = operation;
        HeapStatus = heapStatus;
        MappedAllocationStart = mappedStart;
        MappedAllocationEndExclusive = mappedEnd;
        PhysicalAllocationStart = physicalStart;
        PhysicalAllocationEndExclusive = physicalEnd;
        ObjectReference = objectReference;
        MethodTableToken = methodTableToken;
        BytesConsumed = bytesConsumed;
        UsedBefore = usedBefore;
        UsedAfter = usedAfter;
        AllocationCount = allocationCount;
    }

    public RhpNewFastStatus Status
    {
        get;
    }
    public RhpNewFastOperation Operation
    {
        get;
    }
    public ManagedHeapPageRangeStatus HeapStatus
    {
        get;
    }
    public ulong MappedAllocationStart
    {
        get;
    }
    public ulong MappedAllocationEndExclusive
    {
        get;
    }
    public ulong PhysicalAllocationStart
    {
        get;
    }
    public ulong PhysicalAllocationEndExclusive
    {
        get;
    }
    public ulong ObjectReference
    {
        get;
    }
    public ulong MethodTableToken
    {
        get;
    }
    public ulong BytesConsumed
    {
        get;
    }
    public ulong UsedBefore
    {
        get;
    }
    public ulong UsedAfter
    {
        get;
    }
    public ulong AllocationCount
    {
        get;
    }
    public bool Succeeded => Status == RhpNewFastStatus.Success;
    public bool AllocationSucceeded => Succeeded && Operation == RhpNewFastOperation.Allocation && BytesConsumed != 0;
}
