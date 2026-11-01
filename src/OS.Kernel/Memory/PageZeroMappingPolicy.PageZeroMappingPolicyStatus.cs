namespace OS.Kernel.Memory;
/// <summary>
/// Stable, fail-closed status contract for page-zero mapping intent and
/// caller-owned x64 table-image audits. Values are append-only.
/// </summary>

public enum PageZeroMappingPolicyStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidIntentState = 2,
    InvalidPresentFlag = 3,
    ZeroLength = 4,
    PresentRangeCoversPageZero = 5,
    RangeWrapsThroughPageZero = 6,
    MisalignedRangeStart = 7,
    MisalignedByteLength = 8,
    NonCanonicalRange = 9,
    ContractLayoutMismatch = 10,
    InvalidRootTable = 11,
    InvalidPreconditions = 12,
    InvalidPageImageCount = 13,
    TooManyPageImages = 14,
    PageImageCapacityTooSmall = 15,
    PageImageCapacityExceedsLimit = 16,
    MissingPageImages = 17,
    MisalignedPageImages = 18,
    PageImageSpanOverflow = 19,
    InvalidEntryCount = 20,
    EntryCountMismatch = 21,
    EntryCapacityTooSmall = 22,
    EntryCapacityExceedsLimit = 23,
    MissingEntries = 24,
    MisalignedEntries = 25,
    EntrySpanOverflow = 26,
    StorageOverlap = 27,
    InvalidPageImageRecord = 28,
    PageImageOrderInvalid = 29,
    RootPageNotFound = 30,
    MalformedEntry = 31,
    TablePointerTargetMissing = 32,
    PageZeroPresent = 33
}
