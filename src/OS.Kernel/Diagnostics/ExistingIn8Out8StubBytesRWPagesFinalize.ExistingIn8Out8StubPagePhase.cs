namespace OS.Kernel.Diagnostics;

public enum ExistingIn8Out8StubPagePhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2,
    FinalizationAudited = 3
}
#if !NOSTDLIB_EFI
#endif
