namespace OS.Std;

/// <summary>Allocation-free xorshift32 for repeatable probes, never entropy or security.</summary>
public static class DeterministicRandom32
{
    // Zero selects this fixed nonzero state because xorshift's zero state is absorbing.
    public const uint DefaultSeed = 0x6D2B79F5;

    public static uint Next(ref uint state)
    {
        uint value = state == 0 ? DefaultSeed : state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }
}
