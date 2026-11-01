namespace OS.Kernel.Memory;
/// <summary>
/// Stable semantic instruction identifiers for the paging-mode observation
/// stub. Values are append-only. They describe the independently audited
/// instruction plan; they are not decoded from the emitted byte stream.
/// </summary>

public enum X64PagingModePreflightInstructionKind : byte
{
    Uninitialized = 0,
    PreserveNonvolatileRbx = 1,
    CopyObservationPointer = 2,
    CaptureRflags = 3,
    MoveRflagsToVolatile = 4,
    StoreRflags = 5,
    ClearCompletion = 6,
    ClearReserved = 7,
    ReadCr0 = 8,
    StoreCr0 = 9,
    ReadCr3 = 10,
    StoreCr3 = 11,
    ReadCr4 = 12,
    StoreCr4 = 13,
    SelectEferMsr = 14,
    ReadEferMsr = 15,
    ShiftEferHigh = 16,
    CombineEfer = 17,
    StoreEfer = 18,
    SelectMaximumExtendedLeaf = 19,
    ExecuteCpuidMaximumLeaf = 20,
    StoreMaximumExtendedLeaf = 21,
    CompareAddressSizesLeaf = 22,
    BranchBelowAddressSizesLeaf = 23,
    SelectAddressSizesLeaf = 24,
    ExecuteCpuidAddressSizes = 25,
    StoreAddressSizes = 26,
    BranchPastUnsupportedPath = 27,
    ClearUnsupportedAddressSizes = 28,
    StoreUnsupportedAddressSizes = 29,
    StoreCompletion = 30,
    RestoreNonvolatileRbx = 31,
    Return = 32
}
