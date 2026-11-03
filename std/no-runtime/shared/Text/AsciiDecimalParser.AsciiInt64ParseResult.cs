using OS.Std;

namespace OS.Std.Text;
public readonly struct AsciiInt64ParseResult
{
    private AsciiInt64ParseResult(bool isInitialized, AsciiDecimalParseStatus status, long value, int bytesConsumed, int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }
    public AsciiDecimalParseStatus Status { get; }
    public long Value { get; }
    public int BytesConsumed { get; }
    public int InvalidInputIndex { get; }
    public bool Succeeded => IsInitialized && Status == AsciiDecimalParseStatus.Success && BytesConsumed > 0 && InvalidInputIndex < 0;

    public static AsciiInt64ParseResult Success(long value, int bytesConsumed)
    {
        return new AsciiInt64ParseResult(true, AsciiDecimalParseStatus.Success, value, bytesConsumed, -1);
    }

    public static AsciiInt64ParseResult Fail(AsciiDecimalParseStatus status, int bytesConsumed, int invalidInputIndex)
    {
        return new AsciiInt64ParseResult(true, status, 0, bytesConsumed, invalidInputIndex);
    }
}
