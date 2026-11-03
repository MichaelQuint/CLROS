namespace OS.Boot;
/// <summary>
/// Reports the first failing boundary, or success, while composing the
/// reviewed M01-003, M01-004, and M01-005 contracts. Existing values are
/// append-only and the zero-valued default is intentionally invalid.
/// </summary>

public enum M01LoadedImageStackStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    LoadedImageRejected = 2,
    AllocationTableRejected = 3,
    AllocationEvidenceRejected = 4,
    MapLedgerEvidenceRejected = 5,
    ReservationLedgerRejected = 6,
    ReservationIdentityRejected = 7,
    StackLayoutRejected = 8,
    StackLifetimeRejected = 9,
    StaticStateRejected = 10,
    SwitchPlanRejected = 11,
    StubEmissionRejected = 12,
    StubVerificationRejected = 13,
    HandoffMaterializationRejected = 14,
    StateInitializationRejected = 15,
    ArmRejected = 16,
    TransferRejected = 17,
    ContinuationRspRejected = 18,
    UnexpectedReturn = 19,
    IllegalCoordinatorState = 20,
    StubByteModeRejected = 21,
    LoadedImageVirtualMappingRejected = 22,
    StageOneAddressMappingRejected = 23,
    EvidenceScratchRejected = 24
}
