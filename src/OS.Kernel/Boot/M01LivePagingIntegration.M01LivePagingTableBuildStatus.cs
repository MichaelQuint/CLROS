namespace OS.Boot;
public enum M01LivePagingTableBuildStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidMappingPlan = 2,
    InvalidMappingRangeStorage = 3,
    InvalidTableStorage = 4,
    TableStorageOverlap = 5,
    InvalidRange = 6,
    PageZeroRejected = 7,
    WritableExecutableRejected = 8,
    IdentityAddressMismatch = 9,
    PageImageCreationFailed = 10,
    TablePageCapacityExhausted = 11,
    MalformedExistingEntry = 12,
    MissingChildPage = 13,
    DuplicateLeaf = 14,
    EntryEncodingFailed = 15,
    TablePageCountMismatch = 16,
    MappingPageCountMismatch = 17,
    MissingCr3CodeMapping = 18,
    MissingProtectionProbeMapping = 19,
    MissingTableStateMapping = 20,
    MissingOwnedStackMapping = 21,
    GuardMapped = 22,
    MissingStageOneStubMapping = 23
}
