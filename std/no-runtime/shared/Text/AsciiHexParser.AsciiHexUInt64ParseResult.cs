using OS.Std;

namespace OS.Std.Text;

public readonly struct AsciiHexUInt64ParseResult
{
    private AsciiHexUInt64ParseResult(bool isInitialized, AsciiHexParseStatus status, ulong value, int bytesConsumed, int invalidInputIndex)
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
    public AsciiHexParseStatus Status
    {
        get;
    }
    public ulong Value
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
    public bool Succeeded => IsInitialized && Status == AsciiHexParseStatus.Success && BytesConsumed > 0 && InvalidInputIndex < 0;

    public static AsciiHexUInt64ParseResult Success(ulong value, int bytesConsumed)
    {
        return new AsciiHexUInt64ParseResult(true, AsciiHexParseStatus.Success, value, bytesConsumed, -1);
    }

    public static AsciiHexUInt64ParseResult Fail(AsciiHexParseStatus status, int bytesConsumed, int invalidInputIndex)
    {
        return new AsciiHexUInt64ParseResult(true, status, 0, bytesConsumed, invalidInputIndex);
    }
}
