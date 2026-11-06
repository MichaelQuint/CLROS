namespace OS.Std.Binary;

public readonly struct IndependentEndianReadVector
{
    private readonly bool _isInitialized;
    public IndependentEndianReadVector(byte key, byte firstByte, byte secondByte, ushort expectedLittleEndian, ushort expectedBigEndian)
    {
        _isInitialized = true;
        Key = key;
        FirstByte = firstByte;
        SecondByte = secondByte;
        ExpectedLittleEndian = expectedLittleEndian;
        ExpectedBigEndian = expectedBigEndian;
    }

    public byte Key
    {
        get;
    }
    public byte FirstByte
    {
        get;
    }
    public byte SecondByte
    {
        get;
    }
    public ushort ExpectedLittleEndian
    {
        get;
    }
    public ushort ExpectedBigEndian
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
}
