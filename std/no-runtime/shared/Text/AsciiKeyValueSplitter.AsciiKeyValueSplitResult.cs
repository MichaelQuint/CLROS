using OS.Std;

namespace OS.Std.Text;
public readonly struct AsciiKeyValueSplitResult
{
    private AsciiKeyValueSplitResult(bool isInitialized, AsciiKeyValueSplitStatus status, int sourceStart, int sourceLength, int delimiterIndex, int invalidInputIndex, byte delimiter, AsciiTokenRange key, AsciiTokenRange value)
    {
        IsInitialized = isInitialized;
        Status = status;
        SourceStart = sourceStart;
        SourceLength = sourceLength;
        DelimiterIndex = delimiterIndex;
        InvalidInputIndex = invalidInputIndex;
        Delimiter = delimiter;
        Key = key;
        Value = value;
    }

    public bool IsInitialized { get; }
    public AsciiKeyValueSplitStatus Status { get; }
    public int SourceStart { get; }
    public int SourceLength { get; }
    public int SourceEnd => SourceStart + SourceLength;
    public int DelimiterIndex { get; }
    public int InvalidInputIndex { get; }
    public byte Delimiter { get; }
    public AsciiTokenRange Key { get; }
    public AsciiTokenRange Value { get; }
    public bool Succeeded => IsInitialized && Status == AsciiKeyValueSplitStatus.Success && SourceStart >= 0 && SourceLength >= 0 && SourceEnd >= SourceStart && DelimiterIndex >= SourceStart && DelimiterIndex < SourceEnd && InvalidInputIndex < 0 && Key.Succeeded && Key.Length > 0 && Value.Succeeded && Key.Start >= SourceStart && Key.End <= DelimiterIndex && Value.Start >= DelimiterIndex + 1 && Value.End <= SourceEnd;

    internal static AsciiKeyValueSplitResult Success(int sourceStart, int sourceLength, int delimiterIndex, byte delimiter, AsciiTokenRange key, AsciiTokenRange value)
    {
        return new AsciiKeyValueSplitResult(true, AsciiKeyValueSplitStatus.Success, sourceStart, sourceLength, delimiterIndex, -1, delimiter, key, value);
    }

    internal static AsciiKeyValueSplitResult Fail(AsciiKeyValueSplitStatus status, int sourceStart, int sourceLength, byte delimiter, int invalidInputIndex = -1)
    {
        return new AsciiKeyValueSplitResult(true, status, sourceStart, sourceLength, -1, invalidInputIndex, delimiter, default, default);
    }
}
