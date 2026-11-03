namespace OS.Kernel.Diagnostics;

using OS.Std.Text;

public readonly struct OwnedSerialSmokeMarkerFormatResult
{
    private OwnedSerialSmokeMarkerFormatResult(bool isInitialized, OwnedSerialSmokeMarkerFormatStatus status, int prefixBytes, int tokenBytes, int bytesWritten, int requiredBytes, int availableBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        PrefixBytes = prefixBytes;
        TokenBytes = tokenBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
    }

    public bool IsInitialized
    {
        get;
    }
    public OwnedSerialSmokeMarkerFormatStatus Status
    {
        get;
    }
    public int PrefixBytes
    {
        get;
    }
    public int TokenBytes
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
    public int AvailableBytes
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == OwnedSerialSmokeMarkerFormatStatus.Success && PrefixBytes > 0 && TokenBytes > 0 && RequiredBytes > PrefixBytes && BytesWritten == RequiredBytes && AvailableBytes >= RequiredBytes;

    public static OwnedSerialSmokeMarkerFormatResult Formatted(int prefixBytes, int tokenBytes, int bytesWritten, int availableBytes)
    {
        return new OwnedSerialSmokeMarkerFormatResult(true, OwnedSerialSmokeMarkerFormatStatus.Success, prefixBytes, tokenBytes, bytesWritten, bytesWritten, availableBytes);
    }

    public static OwnedSerialSmokeMarkerFormatResult InvalidToken(int prefixBytes, int tokenBytes, int availableBytes)
    {
        return new OwnedSerialSmokeMarkerFormatResult(true, OwnedSerialSmokeMarkerFormatStatus.InvalidToken, prefixBytes, tokenBytes, 0, 0, availableBytes);
    }

    public static OwnedSerialSmokeMarkerFormatResult DestinationTooSmall(int prefixBytes, int tokenBytes, int requiredBytes, int availableBytes)
    {
        return new OwnedSerialSmokeMarkerFormatResult(true, OwnedSerialSmokeMarkerFormatStatus.DestinationTooSmall, prefixBytes, tokenBytes, 0, requiredBytes, availableBytes);
    }
}
