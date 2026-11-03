using OS.Std;

namespace OS.Std.Text;

public readonly struct AsciiDecimalFormatResult
{
    private AsciiDecimalFormatResult(bool isInitialized, AsciiDecimalFormatStatus status, int destinationLength, int bytesWritten, int requiredBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        DestinationLength = destinationLength;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }
    public AsciiDecimalFormatStatus Status
    {
        get;
    }
    public int DestinationLength
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == AsciiDecimalFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= DestinationLength;

    public static AsciiDecimalFormatResult Success(int destinationLength, int bytesWritten)
    {
        return new AsciiDecimalFormatResult(true, AsciiDecimalFormatStatus.Success, destinationLength, bytesWritten, bytesWritten);
    }

    public static AsciiDecimalFormatResult Fail(AsciiDecimalFormatStatus status, int destinationLength, int requiredBytes)
    {
        return new AsciiDecimalFormatResult(true, status, destinationLength, 0, requiredBytes);
    }
}
