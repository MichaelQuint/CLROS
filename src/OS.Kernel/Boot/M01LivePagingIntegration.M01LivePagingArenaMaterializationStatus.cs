namespace OS.Boot;
/// <summary>
/// Reports the first rejected arena materialization or independent readback
/// boundary. Values are append-only and the zero-valued default is invalid.
/// </summary>

public enum M01LivePagingArenaMaterializationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ContractLayoutMismatch = 2,
    InvalidByteMode = 3,
    InvalidPlan = 4,
    AllocationRecordMismatch = 5,
    InvalidPageImageCount = 6,
    PageImageCapacityTooSmall = 7,
    PageImageCapacityExceedsLimit = 8,
    MissingPageImages = 9,
    MisalignedPageImages = 10,
    PageImageSpanOverflow = 11,
    InvalidEntryCount = 12,
    EntryCountMismatch = 13,
    EntryCapacityTooSmall = 14,
    EntryCapacityExceedsLimit = 15,
    MissingEntries = 16,
    MisalignedEntries = 17,
    EntrySpanOverflow = 18,
    MissingArenaBacking = 19,
    InvalidArenaByteCount = 20,
    ArenaCapacityTooSmall = 21,
    ArenaCapacityExceedsLimit = 22,
    MisalignedArenaBacking = 23,
    ArenaBackingSpanOverflow = 24,
    StorageOverlap = 25,
    PageImageMalformed = 26,
    PageImageOrderInvalid = 27,
    PageImageOutsideTableArena = 28,
    PageImageTargetsStatePage = 29,
    HierarchyMalformed = 30,
    MissingChildPage = 31,
    DuplicateChildOrCycle = 32,
    UnreachablePageImage = 33,
    LeafCountMismatch = 34,
    EntryMismatch = 35,
    StatePageChanged = 36,
    SemanticMappingMismatch = 37,
    MissingStateSnapshot = 38,
    InvalidStateSnapshotByteCount = 39,
    StateSnapshotCapacityTooSmall = 40,
    StateSnapshotCapacityExceedsLimit = 41,
    MisalignedStateSnapshot = 42,
    StateSnapshotSpanOverflow = 43,
    StateSnapshotStorageOverlap = 44
}
