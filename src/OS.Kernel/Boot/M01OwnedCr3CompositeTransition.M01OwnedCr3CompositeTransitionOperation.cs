namespace OS.Boot;

public enum M01OwnedCr3CompositeTransitionOperation : byte
{
    Uninitialized = 0,
    ValidateState = 1,
    ValidateMode = 2,
    DisableInterrupts = 3,
    ClearPge = 4,
    LoadOwnedCr3 = 5,
    UpdateCanary = 6,
    WalkProbeHierarchy = 7,
    CommitProbePte = 8,
    InvalidateProbePage = 9,
    VerifyProbePte = 10,
    InvokeExactProbe = 11,
    AuditPageZero = 12,
    RestoreFirmwareCr3 = 13,
    RestoreCr4 = 14,
    RestoreRflags = 15,
    Return = 16
}
