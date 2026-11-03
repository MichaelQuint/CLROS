namespace OS.Kernel.Memory;

public enum BootPageAllocatorStateStatus
{
    Success = 0,
    FreeListPlanFailed = 1,
    InvalidPageSize = 2,
    NoFreeNodes = 3,
    NoFreePages = 4,
    SourceNodeCapacityTooSmall = 5,
    InvalidHeadNodeIndex = 6,
    InvalidNodePageCount = 7,
    UnalignedNodePhysicalStart = 8,
    NodeByteCountOverflow = 9,
    NodeByteLengthMismatch = 10,
    NodeAddressRangeOverflow = 11,
    NodePhysicalEndMismatch = 12,
    InvalidNextNodeIndex = 13,
    FreeListCycleDetected = 14,
    FreeListNodeCountMismatch = 15,
    TotalPageCountMismatch = 16,
    TotalByteCountMismatch = 17
}
