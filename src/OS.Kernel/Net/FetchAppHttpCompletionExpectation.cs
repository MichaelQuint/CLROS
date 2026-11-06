using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;

public static class FetchAppHttpCompletionExpectation
{
    public const ushort DefaultExpectedHttpStatusCode = 200;
    public const ushort MinimumHttpStatusCode = 100;
    public const ushort MaximumHttpStatusCode = 599;
    public const int MaximumExpectedMarkerLength = 64;
    public static FetchAppHttpCompletionExpectationResult FromManifest(in NativeManifestParseResult manifestParse, ReadOnlySpan<byte> manifestBytes, bool requireConfiguredMarker = false)
    {
        if (!manifestParse.Succeeded)
        {
            return FetchAppHttpCompletionExpectationResult.Fail(FetchAppHttpCompletionExpectationStatus.ManifestNotReady, DefaultExpectedHttpStatusCode, default, GetDefaultExpectedMarker().Length, usesDefaultMarker: true, hasConfiguredStatus: false, hasConfiguredMarker: false);
        }

        NativeManifest manifest = manifestParse.Manifest;
        ushort expectedStatus = DefaultExpectedHttpStatusCode;
        bool hasConfiguredStatus = false;
        bool hasConfiguredMarker = false;
        NativeManifestField markerField = default;
        int markerLength = GetDefaultExpectedMarker().Length;
        for (int index = 0; index < manifest.EnvironmentCount; index++)
        {
            NativeManifestEnvironmentEntry entry = manifest.GetEnvironmentEntry(index);
            if (!entry.TryGetKey(manifestBytes, out ReadOnlySpan<byte> key) || !entry.TryGetValue(manifestBytes, out ReadOnlySpan<byte> value))
            {
                return FetchAppHttpCompletionExpectationResult.Fail(FetchAppHttpCompletionExpectationStatus.ManifestNotReady, expectedStatus, markerField, markerLength, usesDefaultMarker: !hasConfiguredMarker, hasConfiguredStatus, hasConfiguredMarker);
            }

            if (AsciiString.Equals(key, "FETCH_HTTP_EXPECTED_STATUS"u8))
            {
                if (!TryParseStatusCode(value, out expectedStatus))
                {
                    return FetchAppHttpCompletionExpectationResult.Fail(FetchAppHttpCompletionExpectationStatus.InvalidStatusCode, expectedStatus, markerField, markerLength, usesDefaultMarker: !hasConfiguredMarker, hasConfiguredStatus: true, hasConfiguredMarker);
                }

                hasConfiguredStatus = true;
            }
            else if (IsExpectedBodyMarkerKey(key))
            {
                if (value.Length == 0)
                {
                    return FetchAppHttpCompletionExpectationResult.Fail(FetchAppHttpCompletionExpectationStatus.EmptyMarker, expectedStatus, entry.Value, value.Length, usesDefaultMarker: false, hasConfiguredStatus, hasConfiguredMarker: true);
                }

                if (value.Length > MaximumExpectedMarkerLength)
                {
                    return FetchAppHttpCompletionExpectationResult.Fail(FetchAppHttpCompletionExpectationStatus.MarkerTooLarge, expectedStatus, entry.Value, value.Length, usesDefaultMarker: false, hasConfiguredStatus, hasConfiguredMarker: true);
                }

                markerField = entry.Value;
                markerLength = value.Length;
                hasConfiguredMarker = true;
            }
        }

        if (requireConfiguredMarker && !hasConfiguredMarker)
        {
            return FetchAppHttpCompletionExpectationResult.Fail(FetchAppHttpCompletionExpectationStatus.MissingRequiredConfig, expectedStatus, default, 0, usesDefaultMarker: false, hasConfiguredStatus, hasConfiguredMarker: false);
        }

        return FetchAppHttpCompletionExpectationResult.Success(expectedStatus, markerField, markerLength, usesDefaultMarker: !hasConfiguredMarker, hasConfiguredStatus, hasConfiguredMarker);
    }

    public static FetchAppHttpCompletionExpectedMarkerCopyResult CopyExpectedMarker(in FetchAppHttpCompletionExpectationResult expectation, ReadOnlySpan<byte> manifestBytes, Span<byte> destination)
    {
        if (!expectation.Succeeded)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(expectation.Status, bytesWritten: 0, requiredBytes: expectation.ExpectedMarkerLength, availableBytes: destination.Length);
        }

        ReadOnlySpan<byte> marker = expectation.UsesDefaultMarker ? GetDefaultExpectedMarker() : GetConfiguredMarker(in expectation, manifestBytes);
        if (marker.Length == 0)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(FetchAppHttpCompletionExpectationStatus.EmptyMarker, bytesWritten: 0, requiredBytes: expectation.ExpectedMarkerLength, availableBytes: destination.Length);
        }

        if (destination.Length < marker.Length)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(FetchAppHttpCompletionExpectationStatus.DestinationTooSmall, bytesWritten: 0, requiredBytes: marker.Length, availableBytes: destination.Length);
        }

        BufferOperationResult copy = BufferOperations.MemoryCopy(marker, destination);
        if (!copy.Succeeded)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(FetchAppHttpCompletionExpectationStatus.DestinationTooSmall, bytesWritten: 0, requiredBytes: marker.Length, availableBytes: destination.Length);
        }

        return new FetchAppHttpCompletionExpectedMarkerCopyResult(FetchAppHttpCompletionExpectationStatus.Success, bytesWritten: marker.Length, requiredBytes: marker.Length, availableBytes: destination.Length);
    }

    private static bool TryParseStatusCode(ReadOnlySpan<byte> value, out ushort statusCode)
    {
        statusCode = 0;
        AsciiUInt32ParseResult result = AsciiDecimalParser.ParseUInt32(value);
        if (!result.Succeeded || result.Value < MinimumHttpStatusCode || result.Value > MaximumHttpStatusCode)
        {
            return false;
        }

        statusCode = (ushort)result.Value;
        return true;
    }

    private static ReadOnlySpan<byte> GetConfiguredMarker(in FetchAppHttpCompletionExpectationResult expectation, ReadOnlySpan<byte> manifestBytes)
    {
        return expectation.MarkerField.TryGetSlice(manifestBytes, out ReadOnlySpan<byte> marker) ? marker : ReadOnlySpan<byte>.Empty;
    }

    private static ReadOnlySpan<byte> GetDefaultExpectedMarker()
    {
        return "FETCH_OK"u8;
    }

    private static bool IsExpectedBodyMarkerKey(ReadOnlySpan<byte> key)
    {
        return AsciiString.Equals(key, "FETCH_HTTP_EXPECTED_MARKER"u8) || AsciiString.Equals(key, "FETCH_HTTP_EXPECTED_BODY"u8) || AsciiString.Equals(key, "OS_SMOKE_EXPECTED_BODY"u8);
    }
}
