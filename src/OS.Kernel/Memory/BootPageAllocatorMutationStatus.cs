namespace OS.Kernel.Memory;
public enum BootPageAllocatorMutationStatus
{
    Success = 0,
    StateNotInitialized = 1,
    InvalidPageSize = 2,
    InvalidPageCount = 3,
    InvalidAlignment = 4,
    NodeCapacityTooSmall = 5,
    InvalidHeadNodeIndex = 6,
    InvalidNextNodeIndex = 7,
    InvalidNodeShape = 8,
    FreeListCycleDetected = 9,
    FreeListNodeCountMismatch = 10,
    TotalPageCountMismatch = 11,
    TotalByteCountMismatch = 12,
    AllocationByteCountOverflow = 13,
    AllocationAddressOverflow = 14,
    NoSuitableRange = 15,
    MetadataCapacityTooSmall = 16,
    FreeRangeOverlaps = 17,
    FreeRangeNotAllocated = 18,
    ContractLayoutMismatch = 19,
    NodeTableByteCountOverflow = 20,
    TooManyNodes = 21,
    MissingNodes = 22,
    MisalignedNodes = 23,
    NodeTableAddressRangeOverflow = 24,
    ReservationTableByteCountOverflow = 25,
    TooManyReservations = 26,
    ReservationCapacityTooSmall = 27,
    MissingReservations = 28,
    MisalignedReservations = 29,
    ReservationTableAddressRangeOverflow = 30,
    NodeReservationTablesOverlap = 31,
    InvalidStateAccounting = 32,
    InvalidReservation = 33,
    UnsortedReservation = 34,
    OverlappingReservation = 35,
    ReservationOverlapsFreeRange = 36,
    AllocatedPageCountOverflow = 37,
    AllocatedByteCountOverflow = 38,
    AllocationAddressConstructionFailed = 39
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
