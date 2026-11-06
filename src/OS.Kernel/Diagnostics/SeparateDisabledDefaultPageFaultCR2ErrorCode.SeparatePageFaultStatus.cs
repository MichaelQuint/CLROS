namespace OS.Kernel.Diagnostics;

public enum SeparatePageFaultStatus : byte
{
    Uninitialized = 0,
    ReadyDisabled = 1,
    SyntheticObservationAccepted = 2,
    InvalidRequest = 3,
    EnabledByDefaultRejected = 4,
    ExecutionRequestedRejected = 5,
    ExecutionAttemptedRejected = 6,
    NullReadPlanUnavailable = 7,
    OwnedSerialUnavailable = 8,
    OwnedGdtTssUnavailable = 9,
    OwnedIdtUnavailable = 10,
    PageFaultHandlerUnavailable = 11,
    InstructionAddressInPageZero = 12,
    NonCanonicalInstructionAddress = 13,
    VectorMismatch = 14,
    ErrorCodePresenceMismatch = 15,
    ErrorCodeMismatch = 16,
    Cr2Mismatch = 17,
    InstructionAddressMismatch = 18
}
