namespace OS.Boot;

public enum M01OwnedCr3CompositeTransitionResultPhase : byte
{
    Uninitialized = 0,
    StatePrepared = 1,
    Materialized = 2,
    ReadbackVerified = 3,
    ObservedAudit = 4
}
#if !NOSTDLIB_EFI
#endif
