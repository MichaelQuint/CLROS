namespace OS.Pal;
public sealed class OSHostRandomTable
{
    private readonly byte[] seed;
    private readonly int maxRequestBytes;
    private uint state;
    private uint requestSequence;
    public OSHostRandomTable(ReadOnlySpan<byte> seed, int maxRequestBytes)
    {
        if (seed.Length == 0 || maxRequestBytes <= 0)
        {
            this.seed = [];
            this.maxRequestBytes = 0;
            state = 0;
            return;
        }

        this.seed = seed.ToArray();
        this.maxRequestBytes = maxRequestBytes;
        state = CreateInitialState(seed);
    }

    public int MaxRequestBytes => maxRequestBytes;
    public uint RequestCount => requestSequence;

    public OSHostRandomBytesResult FillRandomBytes(Span<byte> destination, uint flags)
    {
        if (flags != OSHostRandomConstants.FlagNone || destination.Length == 0)
        {
            return OSHostRandomBytesResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        if (seed.Length == 0)
        {
            return OSHostRandomBytesResult.FromStatus(OSHostStatus.TryAgain);
        }

        if (destination.Length > maxRequestBytes)
        {
            return OSHostRandomBytesResult.FromStatus(OSHostStatus.BufferTooSmall);
        }

        uint sequence = requestSequence;
        uint local = state + (sequence * 0x9E3779B9u);
        for (int index = 0; index < destination.Length; index++)
        {
            local ^= seed[(int)((sequence + (uint)index) % (uint)seed.Length)];
            local ^= local << 13;
            local ^= local >> 17;
            local ^= local << 5;
            destination[index] = (byte)local;
        }

        state = local == 0 ? 0xA5A5A5A5u : local;
        requestSequence++;
        return new OSHostRandomBytesResult(OSHostStatus.Ok, (uint)destination.Length, sequence);
    }

    private static uint CreateInitialState(ReadOnlySpan<byte> seed)
    {
        uint value = 0x6D2B79F5u;
        for (int index = 0; index < seed.Length; index++)
        {
            value ^= seed[index];
            value = (value << 7) | (value >> 25);
            value *= 0x45D9F3Bu;
        }

        return value == 0 ? 0x6D2B79F5u : value;
    }
}
