namespace OS.Kernel.Memory;
/// <summary>
/// Exact modeled order for the future RW+NX to RO+X boundary.
/// </summary>

public enum X64ProtectionProbeEventKind : byte
{
    Uninitialized = 0,
    StubMaterialized = 1,
    StubReadBackVerified = 2,
    InitialReadWriteNoExecuteVerified = 3,
    ProtectionChangedReadExecute = 4,
    LeafReadBackVerified = 5,
    TargetInvalidationPlanned = 6,
    InvlpgInvocationPlanned = 7,
    ProbeInvocationPlanned = 8,
    ConstantReturnAndCanaryObserved = 9
}
