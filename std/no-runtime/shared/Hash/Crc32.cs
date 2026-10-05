namespace OS.Std.Hash;

public static class Crc32
{
    public const uint ReflectedIeeePolynomial = 0xEDB88320u;
    public const uint InitialState = 0xFFFFFFFFu;
    public const uint FinalXor = 0xFFFFFFFFu;

#if !NOSTDLIB_EFI
    public static uint Compute(ReadOnlySpan<byte> source)
    {
        return Finish(Update(InitialState, source));
    }

    public static uint Update(uint state, ReadOnlySpan<byte> source)
    {
        uint crc = state;

        for (int i = 0; i < source.Length; i++)
        {
            crc = UpdateByte(crc, source[i]);
        }

        return crc;
    }
#endif

    // The no-stdlib entry point needs no span/runtime surface. It accepts every
    // byte/state, does not allocate or throw, and composes with InitialState/Finish.
    public static uint UpdateByte(uint state, byte value)
    {
        uint crc = state ^ value;
        for (int bit = 0; bit < 8; bit++)
        {
            uint mask = 0u - (crc & 1u);
            crc = (crc >> 1) ^ (ReflectedIeeePolynomial & mask);
        }
        return crc;
    }

    public static uint Finish(uint state)
    {
        return state ^ FinalXor;
    }
}
