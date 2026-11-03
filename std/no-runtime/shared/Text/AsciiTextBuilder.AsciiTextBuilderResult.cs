using OS.Std;
using OS.Std.Memory;

namespace OS.Std.Text;
public readonly struct AsciiTextBuilderResult
{
    private AsciiTextBuilderResult(bool isInitialized, AsciiTextBuilderStatus status, int initialOffset, int destinationLength, int bytesWritten, int requiredBytes, int nextOffset, int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        InitialOffset = initialOffset;
        DestinationLength = destinationLength;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        NextOffset = nextOffset;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }
    public AsciiTextBuilderStatus Status { get; }
    public int InitialOffset { get; }
    public int DestinationLength { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int NextOffset { get; }
    public int InvalidInputIndex { get; }
    public bool Succeeded => IsInitialized && Status == AsciiTextBuilderStatus.Success && BytesWritten >= 0 && InitialOffset >= 0 && NextOffset == InitialOffset + BytesWritten && RequiredBytes == NextOffset && RequiredBytes <= DestinationLength;

    public static AsciiTextBuilderResult Success(int initialOffset, int destinationLength, int bytesWritten)
    {
        int nextOffset = initialOffset + bytesWritten;
        return new AsciiTextBuilderResult(true, AsciiTextBuilderStatus.Success, initialOffset, destinationLength, bytesWritten, nextOffset, nextOffset, -1);
    }

    public static AsciiTextBuilderResult Fail(AsciiTextBuilderStatus status, int initialOffset, int destinationLength, int requiredBytes, int invalidInputIndex = -1)
    {
        return new AsciiTextBuilderResult(true, status, initialOffset, destinationLength, 0, requiredBytes, initialOffset, invalidInputIndex);
    }
}
