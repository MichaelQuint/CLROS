namespace OS.Std.Memory;

public readonly struct SpanMemoryVectorExecutionRecord
{
    private readonly bool _isInitialized;
    private SpanMemoryVectorExecutionRecord(byte key, SpanMemoryConformanceOperation operation, SpanMemoryVectorExecutionStatus status, byte outputByteCount, byte resultElementCount, ulong rawUnsignedValue, ulong outputBytes, ulong scratchAfter, bool rejectedInputPreserved)
    {
        _isInitialized = true;
        Key = key;
        Operation = operation;
        Status = status;
        OutputByteCount = outputByteCount;
        ResultElementCount = resultElementCount;
        RawUnsignedValue = rawUnsignedValue;
        OutputBytes = outputBytes;
        ScratchAfter = scratchAfter;
        RejectedInputPreserved = rejectedInputPreserved;
    }

    public byte Key
    {
        get;
    }
    public SpanMemoryConformanceOperation Operation
    {
        get;
    }
    public SpanMemoryVectorExecutionStatus Status
    {
        get;
    }
    public byte OutputByteCount
    {
        get;
    }
    public byte ResultElementCount
    {
        get;
    }
    public ulong RawUnsignedValue
    {
        get;
    }
    public ulong OutputBytes
    {
        get;
    }
    public ulong ScratchAfter
    {
        get;
    }
    public bool RejectedInputPreserved
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
    public bool Succeeded => _isInitialized && (Status == SpanMemoryVectorExecutionStatus.Success || (Status == SpanMemoryVectorExecutionStatus.ExpectedRejection && RejectedInputPreserved));

    internal static SpanMemoryVectorExecutionRecord Create(SpanMemoryConformanceVector vector, SpanMemoryVectorExecutionStatus status, byte outputByteCount, byte resultElementCount, ulong rawUnsignedValue, ulong outputBytes, ulong scratchAfter, bool rejectedInputPreserved)
    {
        return new SpanMemoryVectorExecutionRecord(vector.Key, vector.Operation, status, outputByteCount, resultElementCount, rawUnsignedValue, outputBytes, scratchAfter, rejectedInputPreserved);
    }
}
