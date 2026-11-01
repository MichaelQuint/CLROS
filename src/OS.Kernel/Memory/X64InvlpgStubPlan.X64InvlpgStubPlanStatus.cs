namespace OS.Kernel.Memory;
/// <summary>
/// Stable status contract for the audited INVLPG leaf. Values are append-only.
/// </summary>

public enum X64InvlpgStubPlanStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidTargetRecord = 2,
    TargetOrdinalMustBeZero = 3,
    InvalidCodePage = 4,
    InvalidCodePageLength = 5,
    NonCanonicalCodePage = 6,
    InvalidEntryOffset = 7,
    MisalignedEntry = 8,
    EntryOutsideCodePage = 9,
    MissingDestination = 10,
    MisalignedDestination = 11,
    DestinationCapacityTooSmall = 12,
    DestinationCapacityExceedsPage = 13,
    DestinationSpanOverflow = 14,
    DestinationAddressMismatch = 15,
    MissingSource = 16,
    MisalignedSource = 17,
    SourceCapacityTooSmall = 18,
    SourceCapacityExceedsPage = 19,
    SourceSpanOverflow = 20,
    SourceAddressMismatch = 21,
    InvalidExpectedPlan = 22,
    ExpectedPlanNotMaterialized = 23,
    ByteReadbackMismatch = 24,
    InvalidInvocationRequest = 25,
    InvocationCountMustBeOne = 26,
    InvocationEntryMismatch = 27,
    InvocationTargetMismatch = 28,
    InvalidCodeProtection = 29,
    WritableExecutableCodeRejected = 30,
    ContractLayoutMismatch = 31
}
