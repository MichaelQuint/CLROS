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

    public static ushort Compute(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third)
    {
        InternetChecksumAccumulator accumulator = default;
        accumulator.Update(first);
        accumulator.Update(second);
        accumulator.Update(third);
        return accumulator.Finish();
    }

    public static ushort Compute(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third,
        ReadOnlySpan<byte> fourth)
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

public struct InternetChecksumAccumulator
{
    private uint _sum;
    private byte _pendingHighByte;
    private bool _hasPendingHighByte;

    public uint Sum => _sum;

    public byte PendingHighByte => _pendingHighByte;

    public bool HasPendingHighByte => _hasPendingHighByte;

    public void Reset()
    {
        _sum = 0;
        _pendingHighByte = 0;
        _hasPendingHighByte = false;
    }

    public void Update(ReadOnlySpan<byte> source)
    {
        int index = 0;

        if (_hasPendingHighByte)
        {
            if (source.Length == 0)
            {
                return;
            }

            _sum = InternetChecksum.AddWord(_sum, (ushort)((_pendingHighByte << 8) | source[0]));
            _pendingHighByte = 0;
            _hasPendingHighByte = false;
            index = 1;
        }

        while (index + 1 < source.Length)
        {
            _sum = InternetChecksum.AddWord(_sum, (ushort)((source[index] << 8) | source[index + 1]));
            index += 2;
        }

        if (index < source.Length)
        {
            _pendingHighByte = source[index];
            _hasPendingHighByte = true;
        }
    }

    public ushort Finish()
    {
        uint sum = _sum;

        if (_hasPendingHighByte)
        {
            sum = InternetChecksum.AddWord(sum, (ushort)(_pendingHighByte << 8));
        }

        return InternetChecksum.Finish(sum);
    }

    public bool IsValid()
    {
        return Finish() == InternetChecksum.ValidChecksumResult;
    }
}
