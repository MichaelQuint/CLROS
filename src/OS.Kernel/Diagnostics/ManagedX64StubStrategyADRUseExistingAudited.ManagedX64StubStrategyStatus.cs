namespace OS.Kernel.Diagnostics;

public enum ManagedX64StubStrategyStatus : byte
{
    Success = 0,
    MissingDemonstratedFamily = 1,
    GeneratorThresholdTooLow = 2,
    Out8ByteLengthMismatch = 3,
    In8ByteLengthMismatch = 4,
    WriteThenExecuteRequired = 5,
    PermanentRwxForbidden = 6,
    AuditedBytePlanContractMismatch = 7
}
