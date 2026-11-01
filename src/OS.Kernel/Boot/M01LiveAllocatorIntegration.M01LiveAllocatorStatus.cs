namespace OS.Boot;
/// <summary>
/// Reports the first failing boundary, or success, while composing the
/// reviewed M01 allocator leaves. Existing values are append-only and the
/// zero-valued default is intentionally invalid.
/// </summary>

public enum M01LiveAllocatorStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    StageOneRejected = 2,
    CoordinatorStateRejected = 3,
    DuplicatePhase = 4,
    AllocationTableRejected = 5,
    AllocationEvidenceRejected = 6,
    ArenaAllocationRejected = 7,
    MapLedgerEvidenceRejected = 8,
    ReservationLedgerRejected = 9,
    ArenaReservationRejected = 10,
    SourceRangeRejected = 11,
    ReservationSubtractionRejected = 12,
    FreeRangeRejected = 13,
    StateMaterializationRejected = 14,
    AllocationRejected = 15,
    AllocationContainmentRejected = 16,
    FreeRejected = 17,
    FreeAccountingRejected = 18,
    DoubleFreeNotRejected = 19,
    IllegalCoordinatorState = 20
}
