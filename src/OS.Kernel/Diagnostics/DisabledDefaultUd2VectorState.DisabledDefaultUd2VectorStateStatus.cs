namespace OS.Kernel.Diagnostics;
/// <summary>
/// Stable status contract for the disabled-by-default M02-013 host leaf.
/// Values are append-only. ReadyDisabled and ObservationAccepted are host
/// metadata results only; neither status is guest execution evidence.
/// </summary>

public enum DisabledDefaultUd2VectorStateStatus : byte
{
    Uninitialized = 0,
    ReadyDisabled = 1,
    ObservationAccepted = 2,
    InvalidRequestState = 3,
    EnabledByDefaultRejected = 4,
    ExecutionRequestedRejected = 5,
    ExecutionAttemptedRejected = 6,
    OwnedSerialUnavailable = 7,
    OwnedGdtTssUnavailable = 8,
    OwnedIdtUnavailable = 9,
    InvalidOpcodeHandlerUnavailable = 10,
    NullUd2Address = 11,
    NonCanonicalUd2Address = 12,
    Ud2AddressSpanOverflow = 13,
    NonCanonicalUd2AddressSpan = 14,
    InvalidExpectedState = 15,
    VectorMismatch = 16,
    ErrorCodeMetadataMismatch = 17,
    ErrorCodeMismatch = 18,
    RegisterStateMismatch = 19
}
