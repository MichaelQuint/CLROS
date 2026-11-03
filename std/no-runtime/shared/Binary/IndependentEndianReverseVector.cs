namespace OS.Std.Binary;

public readonly struct IndependentEndianReverseVector
{
    private readonly bool _isInitialized;
    public IndependentEndianReverseVector(byte key, ushort input, ushort expected)
    {
        _isInitialized = true;
        Key = key;
        Input = input;
        Expected = expected;
    }

    public byte Key
    {
        get;
    }
    public ushort Input
    {
        get;
    }
    public ushort Expected
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
}
