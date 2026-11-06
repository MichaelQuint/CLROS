using OS.Kernel.Platform;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public static class OwnedSerialDiagnostics
{
    public const int MaximumMarkerBytes = 256;
    public const int LineEndingBytes = 2;
    public const byte CarriageReturn = 0x0d;
    public const byte LineFeed = 0x0a;
    public static ReadOnlySpan<byte> TestPassOwnedSerialPrefix => OwnedSerialSmokeMarkers.TestPassPrefix;
    public static ReadOnlySpan<byte> PanicPreamblePrefix => OwnedSerialSmokeMarkers.PanicPreamblePrefix;

    public static OwnedSerialMarkerFormatResult FormatMarkerLine(ReadOnlySpan<byte> marker, Span<byte> destination)
    {
        if (!IsValidMarker(marker))
        {
            return OwnedSerialMarkerFormatResult.InvalidMarker(marker.Length);
        }

        int requiredBytes = marker.Length + LineEndingBytes;
        if (destination.Length < requiredBytes)
        {
            return OwnedSerialMarkerFormatResult.DestinationTooSmall(marker.Length, requiredBytes);
        }

        int offset = 0;
        AsciiTextBuilderResult builder = AsciiTextBuilder.AppendBytes(destination, offset, marker);
        if (!builder.Succeeded)
        {
            return MapBuilderFailure(marker.Length, requiredBytes, builder);
        }

        offset = builder.NextOffset;
        builder = AsciiTextBuilder.AppendByte(destination, offset, CarriageReturn);
        if (!builder.Succeeded)
        {
            return MapBuilderFailure(marker.Length, requiredBytes, builder);
        }

        offset = builder.NextOffset;
        builder = AsciiTextBuilder.AppendByte(destination, offset, LineFeed);
        if (!builder.Succeeded)
        {
            return MapBuilderFailure(marker.Length, requiredBytes, builder);
        }

        return OwnedSerialMarkerFormatResult.Formatted(marker.Length);
    }

    public static OwnedSerialDiagnosticWriteResult WriteMarkerLine(IPlatformSerial serial, ReadOnlySpan<byte> marker)
    {
        Span<byte> buffer = stackalloc byte[MaximumMarkerBytes + LineEndingBytes];
        OwnedSerialMarkerFormatResult format = FormatMarkerLine(marker, buffer);
        if (!format.Succeeded)
        {
            return OwnedSerialDiagnosticWriteResult.MarkerFormatFailed(format);
        }

        if (serial is null)
        {
            return OwnedSerialDiagnosticWriteResult.MissingSerial(format);
        }

        PlatformSerialWriteResult serialResult = serial.Write(buffer.Slice(0, format.BytesWritten));
        if (!serialResult.Succeeded)
        {
            return OwnedSerialDiagnosticWriteResult.SerialWriteFailed(format, serialResult);
        }

        return OwnedSerialDiagnosticWriteResult.Success(format, serialResult);
    }

    private static bool IsValidMarker(ReadOnlySpan<byte> marker)
    {
        return OwnedSerialSmokeMarkers.IsValidMarker(marker, MaximumMarkerBytes);
    }

    private static OwnedSerialMarkerFormatResult MapBuilderFailure(int markerBytes, int requiredBytes, AsciiTextBuilderResult builder)
    {
        if (builder.Status == AsciiTextBuilderStatus.NonAsciiInput)
        {
            return OwnedSerialMarkerFormatResult.InvalidMarker(markerBytes);
        }

        return OwnedSerialMarkerFormatResult.DestinationTooSmall(markerBytes, requiredBytes);
    }
}
