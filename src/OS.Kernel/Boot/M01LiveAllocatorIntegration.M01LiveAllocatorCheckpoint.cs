namespace OS.Boot;
/// <summary>
/// Identifies the last successfully completed host-modeled stage-two
/// checkpoint. These values do not report guest execution.
/// </summary>

public enum M01LiveAllocatorCheckpoint : byte
{
    Uninitialized = 0,
    StageOneValidated = 1,
    ArenaAllocationValidated = 2,
    MapLedgerValidated = 3,
    ReservationsValidated = 4,
    FreeListMaterialized = 5,
    PageAllocated = 6,
    PageContained = 7,
    PageFreed = 8,
    DoubleFreeRejected = 9
}
