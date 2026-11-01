namespace OS.Boot;
/// <summary>
/// Identifies the last completed host-audit checkpoint. These values do not
/// claim that firmware allocation or any privileged instruction executed.
/// </summary>

public enum M01LivePagingCheckpoint : byte
{
    Uninitialized = 0,
    StageTwoValidated = 1,
    AllocationsValidated = 2,
    MapLedgerValidated = 3,
    ReservationsValidated = 4,
    IdentityMappingPlanned = 5,
    StubBytesVerified = 6,
    TableImageBuilt = 7,
    PagingModeValidated = 8,
    Cr3PlanValidated = 9,
    Cr3TranscriptValidated = 10,
    ProtectionPlanValidated = 11,
    PageZeroValidated = 12,
    Completed = 13,
    PreparedForCompositeTransition = 14
}
