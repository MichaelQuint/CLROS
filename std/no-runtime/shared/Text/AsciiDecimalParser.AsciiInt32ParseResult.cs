using OS.Std;

namespace OS.Std.Text;

public readonly struct AsciiInt32ParseResult
{
    private AsciiInt32ParseResult(bool isInitialized, AsciiDecimalParseStatus status, int value, int bytesConsumed, int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized
    {
        get;
    }
    public AsciiDecimalParseStatus Status
    {
        get;
    }
    public int Value
    {
        get;
    }
    public int BytesConsumed
    {
        get;
    }
    public int InvalidInputIndex
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == AsciiDecimalParseStatus.Success && BytesConsumed > 0 && InvalidInputIndex < 0;

    public static AsciiInt32ParseResult Success(int value, int bytesConsumed)
    {
        return new AsciiInt32ParseResult(true, AsciiDecimalParseStatus.Success, value, bytesConsumed, -1);
    }

    public static AsciiInt32ParseResult Fail(AsciiDecimalParseStatus status, int bytesConsumed, int invalidInputIndex)
    {
        return new AsciiInt32ParseResult(true, status, 0, bytesConsumed, invalidInputIndex);
    }
}
