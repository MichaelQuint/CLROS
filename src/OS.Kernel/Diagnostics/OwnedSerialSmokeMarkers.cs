namespace OS.Kernel.Diagnostics;

using OS.Std.Text;

public enum OwnedSerialSmokeMarkerFormatStatus
{
    Success = 0,
    InvalidToken = 1,
    DestinationTooSmall = 2
}

public readonly struct OwnedSerialSmokeMarkerFormatResult
{
    private OwnedSerialSmokeMarkerFormatResult(
        bool isInitialized,
        OwnedSerialSmokeMarkerFormatStatus status,
        int prefixBytes,
        int tokenBytes,
        int bytesWritten,
        int requiredBytes,
        int availableBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        PrefixBytes = prefixBytes;
        TokenBytes = tokenBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
    }

    public bool IsInitialized { get; }

    public OwnedSerialSmokeMarkerFormatStatus Status { get; }

    public int PrefixBytes { get; }

    public int TokenBytes { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == OwnedSerialSmokeMarkerFormatStatus.Success &&
        PrefixBytes > 0 &&
        TokenBytes > 0 &&
        RequiredBytes > PrefixBytes &&
        BytesWritten == RequiredBytes &&
        AvailableBytes >= RequiredBytes;

    public static OwnedSerialSmokeMarkerFormatResult Formatted(
        int prefixBytes,
        int tokenBytes,
        int bytesWritten,
        int availableBytes)
    {
        return new OwnedSerialSmokeMarkerFormatResult(
            true,
            OwnedSerialSmokeMarkerFormatStatus.Success,
            prefixBytes,
            tokenBytes,
            bytesWritten,
            bytesWritten,
            availableBytes);
    }

    public static OwnedSerialSmokeMarkerFormatResult InvalidToken(
        int prefixBytes,
        int tokenBytes,
        int availableBytes)
    {
        return new OwnedSerialSmokeMarkerFormatResult(
            true,
            OwnedSerialSmokeMarkerFormatStatus.InvalidToken,
            prefixBytes,
            tokenBytes,
            0,
            0,
            availableBytes);
    }

    public static OwnedSerialSmokeMarkerFormatResult DestinationTooSmall(
        int prefixBytes,
        int tokenBytes,
        int requiredBytes,
        int availableBytes)
    {
        return new OwnedSerialSmokeMarkerFormatResult(
            true,
            OwnedSerialSmokeMarkerFormatStatus.DestinationTooSmall,
            prefixBytes,
            tokenBytes,
            0,
            requiredBytes,
            availableBytes);
    }
}

public static class OwnedSerialSmokeMarkers
{
    public const string TestName = "diagnostics.owned_serial";
    public const string TestStartText = SerialSmokeMarkers.TestStart;
    public const string TestPassText = SerialSmokeMarkers.TestPass;
    public const string TestFailText = SerialSmokeMarkers.TestFail;
    public const string BootOkText = SerialSmokeMarkers.BootOk;
    public const string OsSmokePassText = SerialSmokeMarkers.OsSmokePass;
    public const string OsSmokeFailText = SerialSmokeMarkers.OsSmokeFail;
    public const string TestPassPrefixText = SerialSmokeMarkers.TestPass + " " + TestName;
    public const string PanicPreambleText = OsSmokeFailText;
    public const string DiagnosticsPanicPrefixText =
        PanicPreambleText + " subsystem=diagnostics reason=";

    public const int MaximumTokenBytes = 96;
    public const int MaximumMarkerBytes = 160;

    private const byte Space = 0x20;

    public static ReadOnlySpan<byte> TestStartMarker => "TEST_START"u8;

    public static ReadOnlySpan<byte> TestPassMarker => "TEST_PASS"u8;

    public static ReadOnlySpan<byte> TestFailMarker => "TEST_FAIL"u8;

    public static ReadOnlySpan<byte> BootOkMarker => "OS_BOOT_OK"u8;

    public static ReadOnlySpan<byte> OsSmokePassMarker => "OS_SMOKE_PASS"u8;

    public static ReadOnlySpan<byte> OsSmokeFailMarker => "OS_SMOKE_FAIL"u8;

    public static ReadOnlySpan<byte> TestPassPrefix => "TEST_PASS diagnostics.owned_serial"u8;

    public static ReadOnlySpan<byte> PanicPreamblePrefix => "OS_SMOKE_FAIL"u8;

    public static ReadOnlySpan<byte> DiagnosticsPanicPrefix =>
        "OS_SMOKE_FAIL subsystem=diagnostics reason="u8;

    public static OwnedSerialSmokeMarkerFormatResult FormatTestPassMarker(
        ReadOnlySpan<byte> detail,
        Span<byte> destination)
    {
        return FormatSpacedSuffixMarker(TestPassPrefix, detail, destination);
    }

    public static OwnedSerialSmokeMarkerFormatResult FormatDiagnosticsPanicMarker(
        ReadOnlySpan<byte> reason,
        Span<byte> destination)
    {
        return FormatInlineSuffixMarker(DiagnosticsPanicPrefix, reason, destination);
    }

    public static bool IsTestMarkerPrefix(ReadOnlySpan<byte> markerPrefix)
    {
        return markerPrefix.SequenceEqual(TestStartMarker) ||
            markerPrefix.SequenceEqual(TestPassMarker) ||
            markerPrefix.SequenceEqual(TestFailMarker);
    }

    public static bool HasTestMarkerPrefix(ReadOnlySpan<byte> marker)
    {
        return HasMarkerPrefix(marker, TestStartMarker) ||
            HasMarkerPrefix(marker, TestPassMarker) ||
            HasMarkerPrefix(marker, TestFailMarker);
    }

    public static bool IsKnownSmokeMarker(ReadOnlySpan<byte> marker)
    {
        if (!IsValidMarker(marker))
        {
            return false;
        }

        return marker.SequenceEqual(BootOkMarker) ||
            marker.SequenceEqual(OsSmokePassMarker) ||
            HasMarkerPrefix(marker, OsSmokeFailMarker) ||
            HasTestMarkerPrefix(marker);
    }

    public static bool IsValidMarker(ReadOnlySpan<byte> marker)
    {
        return IsValidMarker(marker, MaximumMarkerBytes);
    }

    public static bool IsValidMarker(ReadOnlySpan<byte> marker, int maximumBytes)
    {
        if (marker.IsEmpty || maximumBytes <= 0 || marker.Length > maximumBytes)
        {
            return false;
        }

        if (marker[0] == Space || marker[marker.Length - 1] == Space)
        {
            return false;
        }

        for (int index = 0; index < marker.Length; index++)
        {
            byte value = marker[index];
            if (value < Space || value > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    private static OwnedSerialSmokeMarkerFormatResult FormatSpacedSuffixMarker(
        ReadOnlySpan<byte> prefix,
        ReadOnlySpan<byte> token,
        Span<byte> destination)
    {
        if (!IsValidToken(token))
        {
            return OwnedSerialSmokeMarkerFormatResult.InvalidToken(
                prefix.Length,
                token.Length,
                destination.Length);
        }

        int requiredBytes = prefix.Length + 1 + token.Length;
        if (destination.Length < requiredBytes)
        {
            return OwnedSerialSmokeMarkerFormatResult.DestinationTooSmall(
                prefix.Length,
                token.Length,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        AppendBytes(prefix, destination, ref offset);
        AppendByte(Space, destination, ref offset);
        AppendBytes(token, destination, ref offset);

        return OwnedSerialSmokeMarkerFormatResult.Formatted(
            prefix.Length,
            token.Length,
            offset,
            destination.Length);
    }

    private static OwnedSerialSmokeMarkerFormatResult FormatInlineSuffixMarker(
        ReadOnlySpan<byte> prefix,
        ReadOnlySpan<byte> token,
        Span<byte> destination)
    {
        if (!IsValidToken(token))
        {
            return OwnedSerialSmokeMarkerFormatResult.InvalidToken(
                prefix.Length,
                token.Length,
                destination.Length);
        }

        int requiredBytes = prefix.Length + token.Length;
        if (destination.Length < requiredBytes)
        {
            return OwnedSerialSmokeMarkerFormatResult.DestinationTooSmall(
                prefix.Length,
                token.Length,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        AppendBytes(prefix, destination, ref offset);
        AppendBytes(token, destination, ref offset);

        return OwnedSerialSmokeMarkerFormatResult.Formatted(
            prefix.Length,
            token.Length,
            offset,
            destination.Length);
    }

    private static bool HasMarkerPrefix(ReadOnlySpan<byte> marker, ReadOnlySpan<byte> prefix)
    {
        return IsValidMarker(marker) &&
            marker.Length >= prefix.Length &&
            marker.Slice(0, prefix.Length).SequenceEqual(prefix) &&
            (marker.Length == prefix.Length || marker[prefix.Length] == Space);
    }

    private static bool IsValidToken(ReadOnlySpan<byte> token)
    {
        if (token.IsEmpty || token.Length > MaximumTokenBytes)
        {
            return false;
        }

        for (int index = 0; index < token.Length; index++)
        {
            byte value = token[index];
            if (value <= Space || value > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendBytes(
        ReadOnlySpan<byte> value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        offset = result.NextOffset;
    }

    private static void AppendByte(byte value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendByte(destination, offset, value);
        offset = result.NextOffset;
    }
}
