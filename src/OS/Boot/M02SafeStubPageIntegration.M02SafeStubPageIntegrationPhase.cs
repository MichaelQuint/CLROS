namespace OS.Boot;

public enum M02SafeStubPageIntegrationPhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2,
    FinalizationAudited = 3,
    ExecutionAudited = 4
}
