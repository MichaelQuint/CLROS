namespace OS.Std.Memory;

public enum BufferOperationStatus
{
    Success = 0,
    DestinationTooSmall = 1
}

public readonly struct BufferOperationResult
{
    private BufferOperationResult(
        bool isInitialized,
        BufferOperationStatus status,
        int sourceLength,
        int destinationLength,
        int bytesCopied,
        int requiredBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        SourceLength = sourceLength;
        DestinationLength = destinationLength;
        BytesCopied = bytesCopied;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized { get; }

    public BufferOperationStatus Status { get; }

    public int SourceLength { get; }

    public int DestinationLength { get; }

    public int BytesCopied { get; }

    public int RequiredBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == BufferOperationStatus.Success &&
        BytesCopied == SourceLength &&
        DestinationLength >= RequiredBytes;

    public static BufferOperationResult Success(
        int sourceLength,
        int destinationLength)
    {
        return new BufferOperationResult(
            true,
            BufferOperationStatus.Success,
            sourceLength,
            destinationLength,
            sourceLength,
            sourceLength);
    }

    public static BufferOperationResult Fail(
        BufferOperationStatus status,
        int sourceLength,
        int destinationLength)
    {
        return new BufferOperationResult(
            true,
            status,
            sourceLength,
            destinationLength,
            0,
            sourceLength);
    }
}

public static class BufferOperations
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;

    /// <summary>Copy borrowed spans, including overlaps, during the caller's
    /// synchronous ownership. No allocation or retained pointer is created;
    /// an undersized destination fails before mutation.</summary>
    public static unsafe BufferOperationResult MemoryCopy(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
    {
        if (source.Length > destination.Length)
        {
            return BufferOperationResult.Fail(
                BufferOperationStatus.DestinationTooSmall,
                source.Length,
                destination.Length);
        }

        if (source.Length == 0)
        {
            return BufferOperationResult.Success(source.Length, destination.Length);
        }

        fixed (byte* sourcePointer = source)
        fixed (byte* destinationPointer = destination)
        {
            nuint sourceStart = (nuint)sourcePointer, destinationStart = (nuint)destinationPointer;
            if (destinationStart > sourceStart && destinationStart - sourceStart < (nuint)source.Length)
            {
                for (int index = source.Length - 1; index >= 0; index--)
                    destinationPointer[index] = sourcePointer[index];
            }
            else
            {
                for (int index = 0; index < source.Length; index++)
                    destinationPointer[index] = sourcePointer[index];
            }
        }

        return BufferOperationResult.Success(source.Length, destination.Length);
    }
}
