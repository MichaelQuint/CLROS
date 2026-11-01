namespace OS.Boot;
/// <summary>
/// Status returned in EAX and recorded by the emitted stream after CLI.
/// Pre-CLI rejection returns one of these values without changing the state.
/// </summary>

public enum M01OwnedCr3CompositeTransitionRuntimeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidState = 2,
    FirmwareCallbackForbidden = 3,
    InvalidCodeRange = 4,
    InvalidStackRange = 5,
    InvalidReturnAddress = 6,
    InvalidTableArena = 7,
    InvalidOwnedCr3 = 8,
    InvalidProbeContract = 9,
    InvalidCanary = 10,
    InvalidMode = 11,
    PhysicalAddressWidthUnsupported = 12,
    InterruptDisableReadbackMismatch = 13,
    PgeClearReadbackMismatch = 14,
    OwnedCr3ReadbackMismatch = 15,
    CanaryBeforeMismatch = 16,
    CanaryAfterMismatch = 17,
    ProbeHierarchyMalformed = 18,
    ProbePteAddressMismatch = 19,
    ProbePteBeforeMismatch = 20,
    ProbePteAfterMismatch = 21,
    ProbeReturnMismatch = 22,
    PageZeroPresent = 23,
    PageZeroMalformed = 24,
    FirmwareCr3ReadbackMismatch = 25,
    Cr4RestoreReadbackMismatch = 26,
    RflagsRestoreReadbackMismatch = 27,
    RspRestoreMismatch = 28,
    PostRestoreCanaryMismatch = 29
}
#if !NOSTDLIB_EFI
#endif
