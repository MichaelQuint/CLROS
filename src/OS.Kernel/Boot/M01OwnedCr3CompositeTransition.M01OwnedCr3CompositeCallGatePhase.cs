namespace OS.Boot;
public enum M01OwnedCr3CompositeCallGatePhase : byte
{
    Uninitialized = 0,
    Prepared = 1,
    Materialized = 2,
    ReadbackVerified = 3,
    ObservedAudit = 4
}
