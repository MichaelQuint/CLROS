namespace OS.Kernel.Memory;
/// <summary>
/// Result of the bounded caller-owned pointer/count/capacity encoder.
/// </summary>

public readonly struct X64PageTableEntryBatchResult
{
    public const ulong StructureSize = 48;
    private readonly ulong _requestCount;
    private readonly ulong _sourceCapacity;
    private readonly ulong _destinationCapacity;
    private readonly ulong _encodedCount;
    private readonly ulong _invalidRequestIndex;
    private readonly byte _isInitialized;
    private readonly X64PageTableEntryStatus _status;
    private X64PageTableEntryBatchResult(X64PageTableEntryStatus status, ulong requestCount, ulong sourceCapacity, ulong destinationCapacity, ulong encodedCount, ulong invalidRequestIndex)
    {
        _requestCount = requestCount;
        _sourceCapacity = sourceCapacity;
        _destinationCapacity = destinationCapacity;
        _encodedCount = encodedCount;
        _invalidRequestIndex = invalidRequestIndex;
        _isInitialized = 1;
        _status = status;
    }

    public bool IsInitialized => _isInitialized == 1;
    public X64PageTableEntryStatus Status => _status;
    public bool Succeeded => IsInitialized && _status == X64PageTableEntryStatus.Success && _encodedCount == _requestCount && _invalidRequestIndex == X64PageTableEntry.NoIndex;
    public ulong RequestCount => _requestCount;
    public ulong SourceCapacity => _sourceCapacity;
    public ulong DestinationCapacity => _destinationCapacity;
    public ulong EncodedCount => _encodedCount;
    public ulong InvalidRequestIndex => _invalidRequestIndex;

    internal static X64PageTableEntryBatchResult Success(ulong requestCount, ulong sourceCapacity, ulong destinationCapacity)
    {
        return new X64PageTableEntryBatchResult(X64PageTableEntryStatus.Success, requestCount, sourceCapacity, destinationCapacity, requestCount, X64PageTableEntry.NoIndex);
    }

    internal static X64PageTableEntryBatchResult Fail(X64PageTableEntryStatus status, ulong requestCount, ulong sourceCapacity, ulong destinationCapacity, ulong invalidRequestIndex)
    {
        return new X64PageTableEntryBatchResult(status, requestCount, sourceCapacity, destinationCapacity, 0, invalidRequestIndex);
    }
}
