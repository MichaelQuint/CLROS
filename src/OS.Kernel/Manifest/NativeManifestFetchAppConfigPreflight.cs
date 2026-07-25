using OS.Net;

namespace OS.Manifest;

public enum NativeManifestFetchAppConfigPreflightStatus
{
    Success = 0,
    ManifestNotReady = 1,
    InvalidStatusCode = 2,
    EmptyMarker = 3,
    MarkerTooLarge = 4,
    MissingRequiredConfig = 5,
    DestinationTooSmall = 6,
    ExpectedMarkerUnavailable = 7
}

public readonly struct NativeManifestFetchAppConfigPreflightResult
{
    private const uint InitializedFlag = 1;
    private const uint UsesDefaultMarkerFlag = 1u << 1;
    private const uint StatusConfiguredFlag = 1u << 2;
    private const uint MarkerConfiguredFlag = 1u << 3;

    private NativeManifestFetchAppConfigPreflightResult(
        NativeManifestFetchAppConfigPreflightStatus status,
        NativeManifestLoadStatus manifestLoadStatus,
        NativeManifestParseStatus manifestParseStatus,
        FetchAppHttpCompletionExpectationStatus expectationStatus,
        ushort expectedHttpStatusCode,
        int expectedMarkerLength,
        int markerBytesWritten,
        int markerRequiredBytes,
        uint manifestBytesRead,
        uint flags)
    {
        Status = status;
        ManifestLoadStatus = manifestLoadStatus;
        ManifestParseStatus = manifestParseStatus;
        ExpectationStatus = expectationStatus;
        ExpectedHttpStatusCode = expectedHttpStatusCode;
        ExpectedMarkerLength = expectedMarkerLength;
        MarkerBytesWritten = markerBytesWritten;
        MarkerRequiredBytes = markerRequiredBytes;
        ManifestBytesRead = manifestBytesRead;
        Flags = flags;
    }

    public NativeManifestFetchAppConfigPreflightStatus Status { get; }

    public NativeManifestLoadStatus ManifestLoadStatus { get; }

    public NativeManifestParseStatus ManifestParseStatus { get; }

    public FetchAppHttpCompletionExpectationStatus ExpectationStatus { get; }

    public ushort ExpectedHttpStatusCode { get; }

    public int ExpectedMarkerLength { get; }

    public int MarkerBytesWritten { get; }

    public int MarkerRequiredBytes { get; }

    public uint ManifestBytesRead { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool UsesDefaultMarker => (Flags & UsesDefaultMarkerFlag) != 0;

    public bool HasConfiguredStatus => (Flags & StatusConfiguredFlag) != 0;

    public bool HasConfiguredMarker => (Flags & MarkerConfiguredFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == NativeManifestFetchAppConfigPreflightStatus.Success &&
        ExpectationStatus == FetchAppHttpCompletionExpectationStatus.Success &&
        ExpectedHttpStatusCode >= FetchAppHttpCompletionExpectation.MinimumHttpStatusCode &&
        ExpectedHttpStatusCode <= FetchAppHttpCompletionExpectation.MaximumHttpStatusCode &&
        ExpectedMarkerLength > 0 &&
        MarkerBytesWritten == ExpectedMarkerLength &&
        MarkerRequiredBytes == ExpectedMarkerLength;

    public static NativeManifestFetchAppConfigPreflightResult Create(
        NativeManifestFetchAppConfigPreflightStatus status,
        NativeManifestLoadStatus manifestLoadStatus,
        NativeManifestParseStatus manifestParseStatus,
        FetchAppHttpCompletionExpectationStatus expectationStatus,
        ushort expectedHttpStatusCode,
        int expectedMarkerLength,
        int markerBytesWritten,
        int markerRequiredBytes,
        uint manifestBytesRead,
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

        return new NativeManifestFetchAppConfigPreflightResult(
            status,
            manifestLoadStatus,
            manifestParseStatus,
            expectationStatus,
            expectedHttpStatusCode,
            expectedMarkerLength,
            markerBytesWritten,
            markerRequiredBytes,
            manifestBytesRead,
            flags);
    }
}

public static class NativeManifestFetchAppConfigPreflight
{
    public static NativeManifestFetchAppConfigPreflightResult Validate(
        in NativeManifestLoadResult manifestLoad,
        ReadOnlySpan<byte> manifestBytes,
        Span<byte> expectedMarkerScratch,
        bool requireConfiguredMarker = false)
    {
        if (!manifestLoad.Succeeded)
        {
            return NativeManifestFetchAppConfigPreflightResult.Create(
                NativeManifestFetchAppConfigPreflightStatus.ManifestNotReady,
                manifestLoad.Status,
                manifestLoad.ManifestStatus,
                FetchAppHttpCompletionExpectationStatus.ManifestNotReady,
                FetchAppHttpCompletionExpectation.DefaultExpectedHttpStatusCode,
                0,
                markerBytesWritten: 0,
                markerRequiredBytes: 0,
                manifestLoad.BytesRead,
                usesDefaultMarker: false,
                hasConfiguredStatus: false,
                hasConfiguredMarker: false);
        }

        NativeManifestParseResult manifestParse = NativeManifestParseResult.Success(
            manifestLoad.Manifest);
        FetchAppHttpCompletionExpectationResult expectation =
            FetchAppHttpCompletionExpectation.FromManifest(
                manifestParse,
                manifestBytes,
                requireConfiguredMarker);

        if (!expectation.Succeeded)
        {
            return NativeManifestFetchAppConfigPreflightResult.Create(
                MapExpectationStatus(expectation.Status),
                manifestLoad.Status,
                manifestLoad.ManifestStatus,
                expectation.Status,
                expectation.ExpectedHttpStatusCode,
                expectation.ExpectedMarkerLength,
                markerBytesWritten: 0,
                markerRequiredBytes: expectation.ExpectedMarkerLength,
                manifestLoad.BytesRead,
                expectation.UsesDefaultMarker,
                expectation.HasConfiguredStatus,
                expectation.HasConfiguredMarker);
        }

        FetchAppHttpCompletionExpectedMarkerCopyResult markerCopy =
            FetchAppHttpCompletionExpectation.CopyExpectedMarker(
                expectation,
                manifestBytes,
                expectedMarkerScratch);
        if (!markerCopy.Succeeded)
        {
            return NativeManifestFetchAppConfigPreflightResult.Create(
                MapExpectationStatus(markerCopy.Status),
                manifestLoad.Status,
                manifestLoad.ManifestStatus,
                markerCopy.Status,
                expectation.ExpectedHttpStatusCode,
                expectation.ExpectedMarkerLength,
                markerCopy.BytesWritten,
                markerCopy.RequiredBytes,
                manifestLoad.BytesRead,
                expectation.UsesDefaultMarker,
                expectation.HasConfiguredStatus,
                expectation.HasConfiguredMarker);
        }

        return NativeManifestFetchAppConfigPreflightResult.Create(
            NativeManifestFetchAppConfigPreflightStatus.Success,
            manifestLoad.Status,
            manifestLoad.ManifestStatus,
            expectation.Status,
            expectation.ExpectedHttpStatusCode,
            expectation.ExpectedMarkerLength,
            markerCopy.BytesWritten,
            markerCopy.RequiredBytes,
            manifestLoad.BytesRead,
            expectation.UsesDefaultMarker,
            expectation.HasConfiguredStatus,
            expectation.HasConfiguredMarker);
    }

    private static NativeManifestFetchAppConfigPreflightStatus MapExpectationStatus(
        FetchAppHttpCompletionExpectationStatus status)
    {
        return status switch
        {
            FetchAppHttpCompletionExpectationStatus.Success =>
                NativeManifestFetchAppConfigPreflightStatus.Success,
            FetchAppHttpCompletionExpectationStatus.ManifestNotReady =>
                NativeManifestFetchAppConfigPreflightStatus.ManifestNotReady,
            FetchAppHttpCompletionExpectationStatus.InvalidStatusCode =>
                NativeManifestFetchAppConfigPreflightStatus.InvalidStatusCode,
            FetchAppHttpCompletionExpectationStatus.EmptyMarker =>
                NativeManifestFetchAppConfigPreflightStatus.EmptyMarker,
            FetchAppHttpCompletionExpectationStatus.MarkerTooLarge =>
                NativeManifestFetchAppConfigPreflightStatus.MarkerTooLarge,
            FetchAppHttpCompletionExpectationStatus.MissingRequiredConfig =>
                NativeManifestFetchAppConfigPreflightStatus.MissingRequiredConfig,
            FetchAppHttpCompletionExpectationStatus.DestinationTooSmall =>
                NativeManifestFetchAppConfigPreflightStatus.DestinationTooSmall,
            _ => NativeManifestFetchAppConfigPreflightStatus.ExpectedMarkerUnavailable
        };
    }
}
