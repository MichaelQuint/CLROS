namespace OS.Std.Net;
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
