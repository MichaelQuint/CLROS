namespace OS.Std.Net;
public static class InternetChecksum
{
    public const ushort ValidChecksumResult = 0;
    public const ushort EmptyChecksum = 0xFFFF;
    public static InternetChecksumAccumulator CreateAccumulator()
    {
        return default;
    }

    public static ushort Compute(ReadOnlySpan<byte> source)
    {
        InternetChecksumAccumulator accumulator = default;
        accumulator.Update(source);
        return accumulator.Finish();
    }

    public static ushort Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        InternetChecksumAccumulator accumulator = default;
        accumulator.Update(first);
        accumulator.Update(second);
        return accumulator.Finish();
    }

    public static ushort Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third)
    {
        InternetChecksumAccumulator accumulator = default;
        accumulator.Update(first);
        accumulator.Update(second);
        accumulator.Update(third);
        return accumulator.Finish();
    }

    public static ushort Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third, ReadOnlySpan<byte> fourth)
    {
        InternetChecksumAccumulator accumulator = default;
        accumulator.Update(first);
        accumulator.Update(second);
        accumulator.Update(third);
        accumulator.Update(fourth);
        return accumulator.Finish();
    }

    public static bool IsValid(ReadOnlySpan<byte> source)
    {
        return Compute(source) == ValidChecksumResult;
    }

    public static ushort Finish(uint sum)
    {
        return (ushort)~Fold(sum);
    }

    public static uint Fold(uint sum)
    {
        while ((sum >> 16) != 0)
        {
            sum = (sum & 0xFFFFu) + (sum >> 16);
        }

        return sum & 0xFFFFu;
    }

    public static uint AddWord(uint sum, ushort word)
    {
        return Fold(sum + word);
    }
}
