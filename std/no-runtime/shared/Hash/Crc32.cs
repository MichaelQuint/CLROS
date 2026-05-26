namespace OS.Std.Hash;

public static class Crc32
{
    public const uint ReflectedIeeePolynomial = 0xEDB88320u;
    public const uint InitialState = 0xFFFFFFFFu;
    public const uint FinalXor = 0xFFFFFFFFu;

    public static uint Compute(ReadOnlySpan<byte> source)
    {
        return Finish(Update(InitialState, source));
    }

    public static uint Update(uint state, ReadOnlySpan<byte> source)
    {
        uint crc = state;

        for (int i = 0; i < source.Length; i++)
        {
            crc ^= source[i];

            for (int bit = 0; bit < 8; bit++)
            {
                uint mask = 0u - (crc & 1u);
                crc = (crc >> 1) ^ (ReflectedIeeePolynomial & mask);
            }
        }

        return crc;
    }

    public static uint Finish(uint state)
    {
        return state ^ FinalXor;
    }
}
