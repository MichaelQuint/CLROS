using OS.Std;

namespace OS.Std.Text;
public readonly struct AsciiUInt64ParseResult
{
    private AsciiUInt64ParseResult(bool isInitialized, AsciiDecimalParseStatus status, ulong value, int bytesConsumed, int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }
    public AsciiDecimalParseStatus Status { get; }
    public ulong Value { get; }
    public int BytesConsumed { get; }
    public int InvalidInputIndex { get; }
    public bool Succeeded => IsInitialized && Status == AsciiDecimalParseStatus.Success && BytesConsumed > 0 && InvalidInputIndex < 0;

    public static AsciiUInt64ParseResult Success(ulong value, int bytesConsumed)
    {
        return new AsciiUInt64ParseResult(true, AsciiDecimalParseStatus.Success, value, bytesConsumed, -1);
    }

    public static AsciiUInt64ParseResult Fail(AsciiDecimalParseStatus status, int bytesConsumed, int invalidInputIndex)
    {
        return new AsciiUInt64ParseResult(true, status, 0, bytesConsumed, invalidInputIndex);
    }
}
