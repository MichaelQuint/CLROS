using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;

public enum FetchAppHttpCompletionExpectationStatus
{
    Success = 0,
    ManifestNotReady = 1,
    InvalidStatusCode = 2,
    EmptyMarker = 3,
    MarkerTooLarge = 4,
    MissingRequiredConfig = 5,
    DestinationTooSmall = 6
}

public readonly struct FetchAppHttpCompletionExpectationResult
{
    private const uint InitializedFlag = 1;
    private const uint UsesDefaultMarkerFlag = 1u << 1;
    private const uint StatusConfiguredFlag = 1u << 2;
    private const uint MarkerConfiguredFlag = 1u << 3;

    private FetchAppHttpCompletionExpectationResult(
        FetchAppHttpCompletionExpectationStatus status,
        ushort expectedHttpStatusCode,
        NativeManifestField markerField,
        int expectedMarkerLength,
        uint flags)
    {
        Status = status;
        ExpectedHttpStatusCode = expectedHttpStatusCode;
        MarkerField = markerField;
        ExpectedMarkerLength = expectedMarkerLength;
        Flags = flags;
    }

    public FetchAppHttpCompletionExpectationStatus Status { get; }

    public ushort ExpectedHttpStatusCode { get; }

    public NativeManifestField MarkerField { get; }

    public int ExpectedMarkerLength { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool UsesDefaultMarker => (Flags & UsesDefaultMarkerFlag) != 0;

    public bool HasConfiguredStatus => (Flags & StatusConfiguredFlag) != 0;

    public bool HasConfiguredMarker => (Flags & MarkerConfiguredFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == FetchAppHttpCompletionExpectationStatus.Success &&
        ExpectedHttpStatusCode >= FetchAppHttpCompletionExpectation.MinimumHttpStatusCode &&
        ExpectedHttpStatusCode <= FetchAppHttpCompletionExpectation.MaximumHttpStatusCode &&
        ExpectedMarkerLength > 0 &&
        ExpectedMarkerLength <= FetchAppHttpCompletionExpectation.MaximumExpectedMarkerLength;

    public static FetchAppHttpCompletionExpectationResult Success(
        ushort expectedHttpStatusCode,
        NativeManifestField markerField,
        int expectedMarkerLength,
        bool usesDefaultMarker,
        bool hasConfiguredStatus,
        bool hasConfiguredMarker)
    {
        uint flags = InitializedFlag;
        if (usesDefaultMarker)
        {
            flags |= UsesDefaultMarkerFlag;
        }

        if (hasConfiguredStatus)
        {
            flags |= StatusConfiguredFlag;
        }

        if (hasConfiguredMarker)
        {
            flags |= MarkerConfiguredFlag;
        }

        return new FetchAppHttpCompletionExpectationResult(
            FetchAppHttpCompletionExpectationStatus.Success,
            expectedHttpStatusCode,
            markerField,
            expectedMarkerLength,
            flags);
    }

    public static FetchAppHttpCompletionExpectationResult Fail(
        FetchAppHttpCompletionExpectationStatus status,
        ushort expectedHttpStatusCode,
        NativeManifestField markerField,
        int expectedMarkerLength,
        bool usesDefaultMarker,
        bool hasConfiguredStatus,
        bool hasConfiguredMarker)
    {
        uint flags = InitializedFlag;
        if (usesDefaultMarker)
        {
            flags |= UsesDefaultMarkerFlag;
        }

        if (hasConfiguredStatus)
        {
            flags |= StatusConfiguredFlag;
        }

        if (hasConfiguredMarker)
        {
            flags |= MarkerConfiguredFlag;
        }

        return new FetchAppHttpCompletionExpectationResult(
            status,
            expectedHttpStatusCode,
            markerField,
            expectedMarkerLength,
            flags);
    }
}

public readonly struct FetchAppHttpCompletionExpectedMarkerCopyResult
{
    private const uint InitializedFlag = 1;

    public FetchAppHttpCompletionExpectedMarkerCopyResult(
        FetchAppHttpCompletionExpectationStatus status,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpCompletionExpectationStatus Status { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == FetchAppHttpCompletionExpectationStatus.Success &&
        BytesWritten == RequiredBytes &&
        RequiredBytes > 0 &&
        RequiredBytes <= AvailableBytes;
}

public static class FetchAppHttpCompletionExpectation
{
    public const ushort DefaultExpectedHttpStatusCode = 200;
    public const ushort MinimumHttpStatusCode = 100;
    public const ushort MaximumHttpStatusCode = 599;
    public const int MaximumExpectedMarkerLength = 64;

    public static FetchAppHttpCompletionExpectationResult FromManifest(
        in NativeManifestParseResult manifestParse,
        ReadOnlySpan<byte> manifestBytes,
        bool requireConfiguredMarker = false)
    {
        if (!manifestParse.Succeeded)
        {
            return FetchAppHttpCompletionExpectationResult.Fail(
                FetchAppHttpCompletionExpectationStatus.ManifestNotReady,
                DefaultExpectedHttpStatusCode,
                default,
                GetDefaultExpectedMarker().Length,
                usesDefaultMarker: true,
                hasConfiguredStatus: false,
                hasConfiguredMarker: false);
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
            if (!entry.TryGetKey(manifestBytes, out ReadOnlySpan<byte> key) ||
                !entry.TryGetValue(manifestBytes, out ReadOnlySpan<byte> value))
            {
                return FetchAppHttpCompletionExpectationResult.Fail(
                    FetchAppHttpCompletionExpectationStatus.ManifestNotReady,
                    expectedStatus,
                    markerField,
                    markerLength,
                    usesDefaultMarker: !hasConfiguredMarker,
                    hasConfiguredStatus,
                    hasConfiguredMarker);
            }

            if (AsciiString.Equals(key, "FETCH_HTTP_EXPECTED_STATUS"u8))
            {
                if (!TryParseStatusCode(value, out expectedStatus))
                {
                    return FetchAppHttpCompletionExpectationResult.Fail(
                        FetchAppHttpCompletionExpectationStatus.InvalidStatusCode,
                        expectedStatus,
                        markerField,
                        markerLength,
                        usesDefaultMarker: !hasConfiguredMarker,
                        hasConfiguredStatus: true,
                        hasConfiguredMarker);
                }

                hasConfiguredStatus = true;
            }
            else if (IsExpectedBodyMarkerKey(key))
            {
                if (value.Length == 0)
                {
                    return FetchAppHttpCompletionExpectationResult.Fail(
                        FetchAppHttpCompletionExpectationStatus.EmptyMarker,
                        expectedStatus,
                        entry.Value,
                        value.Length,
                        usesDefaultMarker: false,
                        hasConfiguredStatus,
                        hasConfiguredMarker: true);
                }

                if (value.Length > MaximumExpectedMarkerLength)
                {
                    return FetchAppHttpCompletionExpectationResult.Fail(
                        FetchAppHttpCompletionExpectationStatus.MarkerTooLarge,
                        expectedStatus,
                        entry.Value,
                        value.Length,
                        usesDefaultMarker: false,
                        hasConfiguredStatus,
                        hasConfiguredMarker: true);
                }

                markerField = entry.Value;
                markerLength = value.Length;
                hasConfiguredMarker = true;
            }
        }

        if (requireConfiguredMarker && !hasConfiguredMarker)
        {
            return FetchAppHttpCompletionExpectationResult.Fail(
                FetchAppHttpCompletionExpectationStatus.MissingRequiredConfig,
                expectedStatus,
                default,
                0,
                usesDefaultMarker: false,
                hasConfiguredStatus,
                hasConfiguredMarker: false);
        }

        return FetchAppHttpCompletionExpectationResult.Success(
            expectedStatus,
            markerField,
            markerLength,
            usesDefaultMarker: !hasConfiguredMarker,
            hasConfiguredStatus,
            hasConfiguredMarker);
    }

    public static FetchAppHttpCompletionExpectedMarkerCopyResult CopyExpectedMarker(
        in FetchAppHttpCompletionExpectationResult expectation,
        ReadOnlySpan<byte> manifestBytes,
        Span<byte> destination)
    {
        if (!expectation.Succeeded)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(
                expectation.Status,
                bytesWritten: 0,
                requiredBytes: expectation.ExpectedMarkerLength,
                availableBytes: destination.Length);
        }

        ReadOnlySpan<byte> marker = expectation.UsesDefaultMarker
            ? GetDefaultExpectedMarker()
            : GetConfiguredMarker(in expectation, manifestBytes);
        if (marker.Length == 0)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(
                FetchAppHttpCompletionExpectationStatus.EmptyMarker,
                bytesWritten: 0,
                requiredBytes: expectation.ExpectedMarkerLength,
                availableBytes: destination.Length);
        }

        if (destination.Length < marker.Length)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(
                FetchAppHttpCompletionExpectationStatus.DestinationTooSmall,
                bytesWritten: 0,
                requiredBytes: marker.Length,
                availableBytes: destination.Length);
        }

        BufferOperationResult copy = BufferOperations.MemoryCopy(marker, destination);
        if (!copy.Succeeded)
        {
            return new FetchAppHttpCompletionExpectedMarkerCopyResult(
                FetchAppHttpCompletionExpectationStatus.DestinationTooSmall,
                bytesWritten: 0,
                requiredBytes: marker.Length,
                availableBytes: destination.Length);
        }

        return new FetchAppHttpCompletionExpectedMarkerCopyResult(
            FetchAppHttpCompletionExpectationStatus.Success,
            bytesWritten: marker.Length,
            requiredBytes: marker.Length,
            availableBytes: destination.Length);
    }

    private static bool TryParseStatusCode(ReadOnlySpan<byte> value, out ushort statusCode)
    {
        statusCode = 0;
        AsciiUInt32ParseResult result = AsciiDecimalParser.ParseUInt32(value);
        if (!result.Succeeded ||
            result.Value < MinimumHttpStatusCode ||
            result.Value > MaximumHttpStatusCode)
        {
            return false;
        }

        statusCode = (ushort)result.Value;
        return true;
    }

    private static ReadOnlySpan<byte> GetConfiguredMarker(
        in FetchAppHttpCompletionExpectationResult expectation,
        ReadOnlySpan<byte> manifestBytes)
    {
        return expectation.MarkerField.TryGetSlice(manifestBytes, out ReadOnlySpan<byte> marker)
            ? marker
            : ReadOnlySpan<byte>.Empty;
    }

    private static ReadOnlySpan<byte> GetDefaultExpectedMarker()
    {
        return "FETCH_OK"u8;
    }

    private static bool IsExpectedBodyMarkerKey(ReadOnlySpan<byte> key)
    {
        return AsciiString.Equals(key, "FETCH_HTTP_EXPECTED_MARKER"u8) ||
            AsciiString.Equals(key, "FETCH_HTTP_EXPECTED_BODY"u8) ||
            AsciiString.Equals(key, "OS_SMOKE_EXPECTED_BODY"u8);
    }
}
