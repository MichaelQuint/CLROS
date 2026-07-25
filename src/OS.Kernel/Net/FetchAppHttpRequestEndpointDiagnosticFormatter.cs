using OS.Std.Text;

namespace OS.Net;

public enum FetchAppHttpRequestEndpointDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct FetchAppHttpRequestEndpointDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public FetchAppHttpRequestEndpointDiagnosticFormatResult(
        FetchAppHttpRequestEndpointDiagnosticFormatStatus status,
        FetchAppHttpRequestEndpointStatus endpointStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        EndpointStatus = endpointStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpRequestEndpointDiagnosticFormatStatus Status { get; }

    public FetchAppHttpRequestEndpointStatus EndpointStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes &&
        RequiredBytes <= AvailableBytes;

    public static FetchAppHttpRequestEndpointDiagnosticFormatResult Fail(
        FetchAppHttpRequestEndpointDiagnosticFormatStatus status,
        FetchAppHttpRequestEndpointStatus endpointStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new FetchAppHttpRequestEndpointDiagnosticFormatResult(
            status,
            endpointStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class FetchAppHttpRequestEndpointDiagnosticFormatter
{
    public static FetchAppHttpRequestEndpointDiagnosticFormatResult FormatResult(
        in FetchAppHttpRequestEndpointResult result,
        Span<byte> destination)
    {
        FetchAppHttpRequestEndpointDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpRequestEndpointDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return FetchAppHttpRequestEndpointDiagnosticFormatResult.Fail(
                FetchAppHttpRequestEndpointDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("FETCH_HTTP_ENDPOINT status="u8, destination, ref offset) ||
            !TryAppendBytes(GetEndpointStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" initialized="u8, destination, ref offset) ||
            !TryAppendBool(result.IsInitialized, destination, ref offset) ||
            !TryAppendBytes(" port="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.Port, destination, ref offset) ||
            !TryAppendBytes(" hostBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.HostBytesWritten, destination, ref offset) ||
            !TryAppendBytes(" pathBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.PathBytesWritten, destination, ref offset) ||
            !TryAppendBytes(" requiredHostBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.RequiredHostBytes, destination, ref offset) ||
            !TryAppendBytes(" requiredPathBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.RequiredPathBytes, destination, ref offset) ||
            !TryAppendBytes(" source="u8, destination, ref offset) ||
            !TryAppendBytes(GetSourceName(in result), destination, ref offset) ||
            !TryAppendBytes(" urlConfigured="u8, destination, ref offset) ||
            !TryAppendBool(result.HasConfiguredUrl, destination, ref offset) ||
            !TryAppendBytes(" hostConfigured="u8, destination, ref offset) ||
            !TryAppendBool(result.HasConfiguredHost, destination, ref offset) ||
            !TryAppendBytes(" portConfigured="u8, destination, ref offset) ||
            !TryAppendBool(result.HasConfiguredPort, destination, ref offset) ||
            !TryAppendBytes(" pathConfigured="u8, destination, ref offset) ||
            !TryAppendBool(result.HasConfiguredPath, destination, ref offset))
        {
            return FetchAppHttpRequestEndpointDiagnosticFormatResult.Fail(
                FetchAppHttpRequestEndpointDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        return new FetchAppHttpRequestEndpointDiagnosticFormatResult(
            FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static FetchAppHttpRequestEndpointDiagnosticFormatResult QueryRequiredBytes(
        in FetchAppHttpRequestEndpointResult result)
    {
        FetchAppHttpRequestEndpointDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpRequestEndpointDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new FetchAppHttpRequestEndpointDiagnosticFormatResult(
            FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static FetchAppHttpRequestEndpointDiagnosticFormatStatus Validate(
        in FetchAppHttpRequestEndpointResult result)
    {
        if (!result.IsInitialized ||
            !IsKnownEndpointStatus(result.Status) ||
            result.HostBytesWritten < 0 ||
            result.PathBytesWritten < 0 ||
            result.RequiredHostBytes < 0 ||
            result.RequiredPathBytes < 0 ||
            result.AvailableHostBytes < 0 ||
            result.AvailablePathBytes < 0)
        {
            return FetchAppHttpRequestEndpointDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == FetchAppHttpRequestEndpointStatus.Success &&
            !result.Succeeded)
        {
            return FetchAppHttpRequestEndpointDiagnosticFormatStatus.InvalidResult;
        }

        return FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in FetchAppHttpRequestEndpointResult result)
    {
        return "FETCH_HTTP_ENDPOINT status="u8.Length
            + GetEndpointStatusName(result.Status).Length
            + " initialized="u8.Length
            + GetBoolByteCount(result.IsInitialized)
            + " port="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(result.Port)
            + " hostBytes="u8.Length
            + AsciiDecimalFormatter.CountInt64Digits(result.HostBytesWritten)
            + " pathBytes="u8.Length
            + AsciiDecimalFormatter.CountInt64Digits(result.PathBytesWritten)
            + " requiredHostBytes="u8.Length
            + AsciiDecimalFormatter.CountInt64Digits(result.RequiredHostBytes)
            + " requiredPathBytes="u8.Length
            + AsciiDecimalFormatter.CountInt64Digits(result.RequiredPathBytes)
            + " source="u8.Length
            + GetSourceName(in result).Length
            + " urlConfigured="u8.Length
            + GetBoolByteCount(result.HasConfiguredUrl)
            + " hostConfigured="u8.Length
            + GetBoolByteCount(result.HasConfiguredHost)
            + " portConfigured="u8.Length
            + GetBoolByteCount(result.HasConfiguredPort)
            + " pathConfigured="u8.Length
            + GetBoolByteCount(result.HasConfiguredPath);
    }

    private static bool IsKnownEndpointStatus(FetchAppHttpRequestEndpointStatus status)
    {
        return status >= FetchAppHttpRequestEndpointStatus.Success &&
            status <= FetchAppHttpRequestEndpointStatus.InvalidUrl;
    }

    private static ReadOnlySpan<byte> GetEndpointStatusName(FetchAppHttpRequestEndpointStatus status)
    {
        return status switch
        {
            FetchAppHttpRequestEndpointStatus.Success => "Success"u8,
            FetchAppHttpRequestEndpointStatus.ManifestNotReady => "ManifestNotReady"u8,
            FetchAppHttpRequestEndpointStatus.UnsupportedScheme => "UnsupportedScheme"u8,
            FetchAppHttpRequestEndpointStatus.MissingHost => "MissingHost"u8,
            FetchAppHttpRequestEndpointStatus.InvalidPort => "InvalidPort"u8,
            FetchAppHttpRequestEndpointStatus.EmptyPath => "EmptyPath"u8,
            FetchAppHttpRequestEndpointStatus.FieldTooLarge => "FieldTooLarge"u8,
            FetchAppHttpRequestEndpointStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            FetchAppHttpRequestEndpointStatus.InvalidUrl => "InvalidUrl"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetSourceName(in FetchAppHttpRequestEndpointResult result)
    {
        if (result.HasConfiguredUrl)
        {
            return "url"u8;
        }

        if (result.HasConfiguredHost || result.HasConfiguredPort || result.HasConfiguredPath)
        {
            return "manifest"u8;
        }

        return "default"u8;
    }

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static bool TryAppendBool(bool value, Span<byte> destination, ref int offset)
    {
        return TryAppendBytes(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static bool TryAppendBytes(
        ReadOnlySpan<byte> value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendInt64Decimal(
        long value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(
        ulong value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
