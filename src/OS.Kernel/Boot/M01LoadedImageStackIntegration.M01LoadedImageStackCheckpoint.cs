namespace OS.Boot;
/// <summary>
/// Identifies the last successfully completed coordinator checkpoint.
/// Values describe host-audited state transitions, not processor execution.
/// </summary>

public enum M01LoadedImageStackCheckpoint : byte
{
    Uninitialized = 0,
    LoadedImageValidated = 1,
    AllocationEvidenceValidated = 2,
    MapLedgerValidated = 3,
    StackLayoutReady = 4,
    SwitchPlanReady = 5,
    StubVerified = 6,
    SwitchArmed = 7,
    TransferRecorded = 8,
    ContinuationRspContained = 9
}
