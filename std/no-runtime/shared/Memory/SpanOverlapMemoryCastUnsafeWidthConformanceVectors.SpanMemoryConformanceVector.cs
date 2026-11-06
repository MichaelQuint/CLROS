namespace OS.Std.Memory;

public readonly struct SpanMemoryConformanceVector
{
    private readonly bool _isInitialized;
    public SpanMemoryConformanceVector(byte key, SpanMemoryConformanceOperation operation, int sourceOffset, int destinationOffset, int byteCount, byte sourceWidth, byte destinationWidth, byte flags, byte expectedByteCount, byte expectedElementCount, ulong input, ulong expected)
    {
        _isInitialized = true;
        Key = key;
        Operation = operation;
        SourceOffset = sourceOffset;
        DestinationOffset = destinationOffset;
        ByteCount = byteCount;
        SourceWidth = sourceWidth;
        DestinationWidth = destinationWidth;
        Flags = flags;
        ExpectedByteCount = expectedByteCount;
        ExpectedElementCount = expectedElementCount;
        Input = input;
        Expected = expected;
    }

    public byte Key
    {
        get;
    }
    public SpanMemoryConformanceOperation Operation
    {
        get;
    }
    public int SourceOffset
    {
        get;
    }
    public int DestinationOffset
    {
        get;
    }
    public int ByteCount
    {
        get;
    }
    public byte SourceWidth
    {
        get;
    }
    public byte DestinationWidth
    {
        get;
    }
    public byte Flags
    {
        get;
    }
    public byte ExpectedByteCount
    {
        get;
    }
    public byte ExpectedElementCount
    {
        get;
    }
    public ulong Input
    {
        get;
    }
    public ulong Expected
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
    public bool ExpectedSuccess => (Flags & SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExpectedSuccessFlag) != 0;
    public bool SignExtend => (Flags & SpanOverlapMemoryCastUnsafeWidthConformanceVectors.SignExtendFlag) != 0;
}
