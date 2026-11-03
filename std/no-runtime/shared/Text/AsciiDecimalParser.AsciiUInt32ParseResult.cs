using OS.Std;

namespace OS.Std.Text;
public readonly struct AsciiUInt32ParseResult
{
    private AsciiUInt32ParseResult(bool isInitialized, AsciiDecimalParseStatus status, uint value, int bytesConsumed, int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }
    public AsciiDecimalParseStatus Status { get; }
    public uint Value { get; }
    public int BytesConsumed { get; }
    public int InvalidInputIndex { get; }
    public bool Succeeded => IsInitialized && Status == AsciiDecimalParseStatus.Success && BytesConsumed > 0 && InvalidInputIndex < 0;

    public static AsciiUInt32ParseResult Success(uint value, int bytesConsumed)
    {
        return new AsciiUInt32ParseResult(true, AsciiDecimalParseStatus.Success, value, bytesConsumed, -1);
    }

    public static AsciiUInt32ParseResult Fail(AsciiDecimalParseStatus status, int bytesConsumed, int invalidInputIndex)
    {
        return new AsciiUInt32ParseResult(true, status, 0, bytesConsumed, invalidInputIndex);
    }
}
