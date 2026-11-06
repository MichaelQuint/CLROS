namespace OS.Boot;
/// <summary>
/// Reports the first rejected boundary while sizing one caller-owned physical
/// arena that contains every fresh table page followed by one state/canary
/// page. Values are append-only and the zero-valued default is invalid.
/// </summary>

public enum M01LivePagingArenaPlanStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ContractLayoutMismatch = 2,
    InvalidFixedRequirementCount = 3,
    InvalidFixedRequirementCapacity = 4,
    MissingFixedRequirements = 5,
    MisalignedFixedRequirements = 6,
    FixedRequirementSpanOverflow = 7,
    InvalidFixedRequirementSet = 8,
    InvalidDescriptor = 9,
    InvalidCandidate = 10,
    InvalidPreconditions = 11,
    InvalidIterationLimit = 12,
    InvalidReservationStorage = 13,
    ReservationValidationFailed = 14,
    InvalidRequirementWorkspace = 15,
    InvalidRangeWorkspace = 16,
    WorkspaceOverlap = 17,
    RequirementConstructionFailed = 18,
    IdentityPlanRejected = 19,
    RangeWorkspaceTooSmall = 20,
    TableCapacityExceeded = 21,
    NonMonotonicIteration = 22,
    IterationLimitReached = 23,
    TablePageCountOverflow = 24,
    ArenaGeometryOverflow = 25,
    PhysicalWidthExceeded = 26,
    DescriptorCapacityTooSmall = 27,
    ReservationOverlap = 28,
    CapacityExceedsLimit = 29
}
