using OS.Kernel.Platform;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public readonly struct OwnedSerialMarkerFormatResult
{
    private OwnedSerialMarkerFormatResult(bool isInitialized, OwnedSerialMarkerFormatStatus status, int markerBytes, int bytesWritten, int requiredBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        MarkerBytes = markerBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }
    public OwnedSerialMarkerFormatStatus Status
    {
        get;
    }
    public int MarkerBytes
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
    public bool Succeeded => IsInitialized && Status == OwnedSerialMarkerFormatStatus.Success && MarkerBytes > 0 && RequiredBytes == MarkerBytes + OwnedSerialDiagnostics.LineEndingBytes && BytesWritten == RequiredBytes;

    public static OwnedSerialMarkerFormatResult Formatted(int markerBytes)
    {
        return new OwnedSerialMarkerFormatResult(true, OwnedSerialMarkerFormatStatus.Success, markerBytes, markerBytes + OwnedSerialDiagnostics.LineEndingBytes, markerBytes + OwnedSerialDiagnostics.LineEndingBytes);
    }

    public static OwnedSerialMarkerFormatResult InvalidMarker(int markerBytes)
    {
        return new OwnedSerialMarkerFormatResult(true, OwnedSerialMarkerFormatStatus.InvalidMarker, markerBytes, 0, 0);
    }

    public static OwnedSerialMarkerFormatResult DestinationTooSmall(int markerBytes, int requiredBytes)
    {
        return new OwnedSerialMarkerFormatResult(true, OwnedSerialMarkerFormatStatus.DestinationTooSmall, markerBytes, 0, requiredBytes);
    }
}
