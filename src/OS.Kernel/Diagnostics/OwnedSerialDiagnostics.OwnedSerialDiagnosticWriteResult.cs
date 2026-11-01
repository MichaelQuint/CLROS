using OS.Kernel.Platform;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public readonly struct OwnedSerialDiagnosticWriteResult
{
    private OwnedSerialDiagnosticWriteResult(bool isInitialized, OwnedSerialDiagnosticWriteStatus status, OwnedSerialMarkerFormatStatus formatStatus, PlatformSerialWriteStatus serialStatus, FirmwareServicesMode mode, PlatformSerialTransportKind transportKind, int markerBytes, int bytesWritten, int requiredBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        FormatStatus = formatStatus;
        SerialStatus = serialStatus;
        Mode = mode;
        TransportKind = transportKind;
        MarkerBytes = markerBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized { get; }
    public OwnedSerialDiagnosticWriteStatus Status { get; }
    public OwnedSerialMarkerFormatStatus FormatStatus { get; }
    public PlatformSerialWriteStatus SerialStatus { get; }
    public FirmwareServicesMode Mode { get; }
    public PlatformSerialTransportKind TransportKind { get; }
    public int MarkerBytes { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public bool Succeeded => IsInitialized && Status == OwnedSerialDiagnosticWriteStatus.Success && FormatStatus == OwnedSerialMarkerFormatStatus.Success && SerialStatus == PlatformSerialWriteStatus.Success && MarkerBytes > 0 && RequiredBytes == MarkerBytes + OwnedSerialDiagnostics.LineEndingBytes && BytesWritten == RequiredBytes;

    public static OwnedSerialDiagnosticWriteResult Success(OwnedSerialMarkerFormatResult format, PlatformSerialWriteResult serial)
    {
        return new OwnedSerialDiagnosticWriteResult(true, OwnedSerialDiagnosticWriteStatus.Success, format.Status, serial.Status, serial.Mode, serial.TransportKind, format.MarkerBytes, serial.BytesWritten, format.RequiredBytes);
    }

    public static OwnedSerialDiagnosticWriteResult MarkerFormatFailed(OwnedSerialMarkerFormatResult format)
    {
        return new OwnedSerialDiagnosticWriteResult(true, OwnedSerialDiagnosticWriteStatus.MarkerFormatFailed, format.Status, PlatformSerialWriteStatus.InvalidPayload, FirmwareServicesMode.BootServicesAvailable, PlatformSerialTransportKind.None, format.MarkerBytes, 0, format.RequiredBytes);
    }

    public static OwnedSerialDiagnosticWriteResult SerialWriteFailed(OwnedSerialMarkerFormatResult format, PlatformSerialWriteResult serial)
    {
        return new OwnedSerialDiagnosticWriteResult(true, OwnedSerialDiagnosticWriteStatus.SerialWriteFailed, format.Status, serial.Status, serial.Mode, serial.TransportKind, format.MarkerBytes, serial.BytesWritten, format.RequiredBytes);
    }

    public static OwnedSerialDiagnosticWriteResult MissingSerial(OwnedSerialMarkerFormatResult format)
    {
        return new OwnedSerialDiagnosticWriteResult(true, OwnedSerialDiagnosticWriteStatus.MissingSerial, format.Status, PlatformSerialWriteStatus.NoOwnedTransport, FirmwareServicesMode.BootServicesAvailable, PlatformSerialTransportKind.None, format.MarkerBytes, 0, format.RequiredBytes);
    }
}
