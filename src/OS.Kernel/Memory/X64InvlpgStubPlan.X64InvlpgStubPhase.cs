namespace OS.Kernel.Memory;
/// <summary>
/// Identifies whether the exact INVLPG byte stream has only been copied or has
/// also passed an independent caller-owned readback.
/// </summary>

public enum X64InvlpgStubPhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2
}
