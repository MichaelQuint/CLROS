namespace OS.Kernel.Memory;
/// <summary>
/// Identifies the caller-owned materialization state of the constant-return
/// protection probe.
/// </summary>

public enum X64ProtectionProbeStubPhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2
}
