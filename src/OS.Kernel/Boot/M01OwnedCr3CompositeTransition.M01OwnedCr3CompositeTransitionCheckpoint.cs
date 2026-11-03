namespace OS.Boot;

public enum M01OwnedCr3CompositeTransitionCheckpoint : byte
{
    Uninitialized = 0,
    EntryValidated = 1,
    ModeValidated = 2,
    InterruptsDisabled = 3,
    PgeCleared = 4,
    OwnedCr3Loaded = 5,
    CanaryUpdated = 6,
    ProbePteLocated = 7,
    ProbePteCommitted = 8,
    ProbeInvalidated = 9,
    ProbePteVerified = 10,
    ProbeReturned = 11,
    PageZeroAbsent = 12,
    FirmwareCr3Restored = 13,
    Cr4Restored = 14,
    RflagsRestored = 15,
    Returned = 16,
    ReturnPrepared = 17
}
#if !NOSTDLIB_EFI
#endif
