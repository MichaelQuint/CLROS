using OS.Std;

namespace OS.Std.Text;
public readonly struct AsciiTokenSplitResult
{
    private AsciiTokenSplitResult(bool isInitialized, AsciiTokenSplitStatus status, int sourceStart, int sourceLength, int tokenCount, int requiredTokenCount, int invalidInputIndex, byte delimiter)
    {
        IsInitialized = isInitialized;
        Status = status;
        SourceStart = sourceStart;
        SourceLength = sourceLength;
        TokenCount = tokenCount;
        RequiredTokenCount = requiredTokenCount;
        InvalidInputIndex = invalidInputIndex;
        Delimiter = delimiter;
    }

    public bool IsInitialized { get; }
    public AsciiTokenSplitStatus Status { get; }
    public int SourceStart { get; }
    public int SourceLength { get; }
    public int TokenCount { get; }
    public int RequiredTokenCount { get; }
    public int InvalidInputIndex { get; }
    public byte Delimiter { get; }
    public bool Succeeded => IsInitialized && Status == AsciiTokenSplitStatus.Success && TokenCount >= 0 && RequiredTokenCount == TokenCount && SourceStart >= 0 && SourceLength >= 0 && InvalidInputIndex < 0;

    public static AsciiTokenSplitResult Success(int sourceStart, int sourceLength, int tokenCount, byte delimiter)
    {
        return new AsciiTokenSplitResult(true, AsciiTokenSplitStatus.Success, sourceStart, sourceLength, tokenCount, tokenCount, -1, delimiter);
    }

    public static AsciiTokenSplitResult Fail(AsciiTokenSplitStatus status, int sourceStart, int sourceLength, int requiredTokenCount, byte delimiter, int invalidInputIndex = -1)
    {
        return new AsciiTokenSplitResult(true, status, sourceStart, sourceLength, 0, requiredTokenCount, invalidInputIndex, delimiter);
    }
}
