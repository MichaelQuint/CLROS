namespace OS.Boot;
// Reference boundary:
// - references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs,
//   local snapshot SHA-256
//   35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC,
//   influenced only the four-level hierarchy walk and table ownership model.
// - references/SharpOS-main/OS/src/Kernel/Paging/Cr3Accessor.cs,
//   local snapshot SHA-256
//   088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47,
//   was used only to cross-check preservation of the complete observed CR3
//   value, including architecturally permitted low bits.
// No reference implementation is copied. This file composes first-party
// CLROS plans over caller-owned POD and never executes privileged bytes.
/// <summary>
/// Reports the first rejected boundary in the third M01 host coordinator.
/// Existing values are append-only and the zero-valued default is invalid.
/// </summary>

public enum M01LivePagingStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    StageTwoRejected = 2,
    AllocationTableRejected = 3,
    AllocationEvidenceRejected = 4,
    MapLedgerEvidenceRejected = 5,
    ReservationLedgerRejected = 6,
    ReservationIdentityRejected = 7,
    CoordinatorStateRejected = 8,
    DuplicatePhase = 9,
    IdentityMappingRejected = 10,
    TableStorageRejected = 11,
    TableConstructionRejected = 12,
    PreflightStubRejected = 13,
    PreflightRejected = 14,
    TransitionStubRejected = 15,
    TransitionEvidenceRejected = 16,
    Cr3PlanRejected = 17,
    Cr3TranscriptRejected = 18,
    InvlpgStubRejected = 19,
    ProtectionProbeStubRejected = 20,
    ProtectionMutationRejected = 21,
    ProtectionProbeRejected = 22,
    PageZeroRejected = 23,
    IllegalCoordinatorState = 24,
    ByteModeRejected = 25,
    CleanupPlanRejected = 26
}
