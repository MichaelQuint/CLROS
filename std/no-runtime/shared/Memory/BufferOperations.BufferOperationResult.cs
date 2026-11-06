namespace OS.Std.Memory;

public readonly struct BufferOperationResult
{
    private BufferOperationResult(bool isInitialized, BufferOperationStatus status, int sourceLength, int destinationLength, int bytesCopied, int requiredBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        SourceLength = sourceLength;
        DestinationLength = destinationLength;
        BytesCopied = bytesCopied;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }
    public BufferOperationStatus Status
    {
        get;
    }
    public int SourceLength
    {
        get;
    }
    public int DestinationLength
    {
        get;
    }
    public int BytesCopied
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == BufferOperationStatus.Success && BytesCopied == SourceLength && DestinationLength >= RequiredBytes;

    public static BufferOperationResult Success(int sourceLength, int destinationLength)
    {
        return new BufferOperationResult(true, BufferOperationStatus.Success, sourceLength, destinationLength, sourceLength, sourceLength);
    }

    public static BufferOperationResult Fail(BufferOperationStatus status, int sourceLength, int destinationLength)
    {
        return new BufferOperationResult(true, status, sourceLength, destinationLength, 0, sourceLength);
    }
}
