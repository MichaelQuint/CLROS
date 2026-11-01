namespace OS.Boot;
public enum M01OwnedCr3CompositeTransitionRecoveryClass : byte
{
    Uninitialized = 0,
    PreCliNoStateMutation = 1,
    RestoreRflags = 2,
    RestoreCr4ThenRflags = 3,
    RestoreCr3ThenCr4ThenRflags = 4,
    CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters = 5,
    SuccessRestoreRegisters = 6,
    MutationAttemptedThenRestoreRegisters = 7
}
#if !NOSTDLIB_EFI
#endif
