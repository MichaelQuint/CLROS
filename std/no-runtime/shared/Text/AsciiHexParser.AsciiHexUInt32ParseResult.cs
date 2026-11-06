using OS.Std;

namespace OS.Std.Text;

public readonly struct AsciiHexUInt32ParseResult
{
    private AsciiHexUInt32ParseResult(bool isInitialized, AsciiHexParseStatus status, uint value, int bytesConsumed, int invalidInputIndex)
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
    public uint Value
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

    public static AsciiHexUInt32ParseResult Success(uint value, int bytesConsumed)
    {
        return new AsciiHexUInt32ParseResult(true, AsciiHexParseStatus.Success, value, bytesConsumed, -1);
    }

    public static AsciiHexUInt32ParseResult Fail(AsciiHexParseStatus status, int bytesConsumed, int invalidInputIndex)
    {
        return new AsciiHexUInt32ParseResult(true, status, 0, bytesConsumed, invalidInputIndex);
    }
}
