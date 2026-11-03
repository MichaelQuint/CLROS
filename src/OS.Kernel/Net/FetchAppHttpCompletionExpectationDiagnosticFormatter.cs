using OS.Std.Text;

namespace OS.Net;

public static class FetchAppHttpCompletionExpectationDiagnosticFormatter
{
    public static FetchAppHttpCompletionExpectationDiagnosticFormatResult FormatResult(in FetchAppHttpCompletionExpectationResult result, Span<byte> destination)
    {
        FetchAppHttpCompletionExpectationDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpCompletionExpectationDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return FetchAppHttpCompletionExpectationDiagnosticFormatResult.Fail(FetchAppHttpCompletionExpectationDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        Append("FETCH_HTTP_EXPECTATION status="u8, destination, ref offset);
        Append(GetExpectationStatusName(result.Status), destination, ref offset);
        Append(" initialized="u8, destination, ref offset);
        AppendBool(result.IsInitialized, destination, ref offset);
        Append(" expectedHttpStatus="u8, destination, ref offset);
        AppendUInt64(result.ExpectedHttpStatusCode, destination, ref offset);
        Append(" expectedMarkerBytes="u8, destination, ref offset);
        AppendInt64(result.ExpectedMarkerLength, destination, ref offset);
        Append(" source="u8, destination, ref offset);
        Append(GetSourceName(in result), destination, ref offset);
        Append(" statusConfigured="u8, destination, ref offset);
        AppendBool(result.HasConfiguredStatus, destination, ref offset);
        Append(" markerConfigured="u8, destination, ref offset);
        AppendBool(result.HasConfiguredMarker, destination, ref offset);
        Append(" defaultMarker="u8, destination, ref offset);
        AppendBool(result.UsesDefaultMarker, destination, ref offset);
        return new FetchAppHttpCompletionExpectationDiagnosticFormatResult(FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static FetchAppHttpCompletionExpectationDiagnosticFormatResult QueryRequiredBytes(in FetchAppHttpCompletionExpectationResult result)
    {
        FetchAppHttpCompletionExpectationDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpCompletionExpectationDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new FetchAppHttpCompletionExpectationDiagnosticFormatResult(FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static FetchAppHttpCompletionExpectationDiagnosticFormatStatus Validate(in FetchAppHttpCompletionExpectationResult result)
    {
        if (!result.IsInitialized || !IsKnownExpectationStatus(result.Status) || result.ExpectedMarkerLength < 0)
        {
            return FetchAppHttpCompletionExpectationDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == FetchAppHttpCompletionExpectationStatus.Success && !result.Succeeded)
        {
            return FetchAppHttpCompletionExpectationDiagnosticFormatStatus.InvalidResult;
        }

        return FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in FetchAppHttpCompletionExpectationResult result)
    {
        return "FETCH_HTTP_EXPECTATION status="u8.Length + GetExpectationStatusName(result.Status).Length + " initialized="u8.Length + GetBoolByteCount(result.IsInitialized) + " expectedHttpStatus="u8.Length + GetUInt64DecimalByteCount(result.ExpectedHttpStatusCode) + " expectedMarkerBytes="u8.Length + GetInt64DecimalByteCount(result.ExpectedMarkerLength) + " source="u8.Length + GetSourceName(in result).Length + " statusConfigured="u8.Length + GetBoolByteCount(result.HasConfiguredStatus) + " markerConfigured="u8.Length + GetBoolByteCount(result.HasConfiguredMarker) + " defaultMarker="u8.Length + GetBoolByteCount(result.UsesDefaultMarker);
    }

    private static bool IsKnownExpectationStatus(FetchAppHttpCompletionExpectationStatus status)
    {
        return status >= FetchAppHttpCompletionExpectationStatus.Success && status <= FetchAppHttpCompletionExpectationStatus.DestinationTooSmall;
    }

    private static ReadOnlySpan<byte> GetExpectationStatusName(FetchAppHttpCompletionExpectationStatus status)
    {
        return status switch
        {
            FetchAppHttpCompletionExpectationStatus.Success => "Success"u8,
            FetchAppHttpCompletionExpectationStatus.ManifestNotReady => "ManifestNotReady"u8,
            FetchAppHttpCompletionExpectationStatus.InvalidStatusCode => "InvalidStatusCode"u8,
            FetchAppHttpCompletionExpectationStatus.EmptyMarker => "EmptyMarker"u8,
            FetchAppHttpCompletionExpectationStatus.MarkerTooLarge => "MarkerTooLarge"u8,
            FetchAppHttpCompletionExpectationStatus.MissingRequiredConfig => "MissingRequiredConfig"u8,
            FetchAppHttpCompletionExpectationStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetSourceName(in FetchAppHttpCompletionExpectationResult result)
    {
        if (result.HasConfiguredMarker || result.HasConfiguredStatus)
        {
            return "manifest"u8;
        }

        return result.UsesDefaultMarker ? "default"u8 : "missing"u8;
    }

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static int GetInt64DecimalByteCount(long value)
    {
        bool negative = value < 0;
        ulong magnitude;
        if (value == long.MinValue)
        {
            magnitude = ((ulong)long.MaxValue) + 1UL;
        }
        else if (negative)
        {
            magnitude = (ulong)(-value);
        }
        else
        {
            magnitude = (ulong)value;
        }

        return GetUInt64DecimalByteCount(magnitude) + (negative ? 1 : 0);
    }

    private static int GetUInt64DecimalByteCount(ulong value)
    {
        int count = 1;
        while (value >= 10)
        {
            value /= 10;
            count++;
        }

        return count;
    }

    private static void Append(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void AppendBool(bool value, Span<byte> destination, ref int offset)
    {
        Append(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static void AppendInt64(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void AppendUInt64(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }
}
