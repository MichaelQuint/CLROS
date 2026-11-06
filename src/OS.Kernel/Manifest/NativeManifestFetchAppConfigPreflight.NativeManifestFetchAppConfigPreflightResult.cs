using OS.Net;

namespace OS.Manifest;

public readonly struct NativeManifestFetchAppConfigPreflightResult
{
    private const uint InitializedFlag = 1;
    private const uint UsesDefaultMarkerFlag = 1u << 1;
    private const uint StatusConfiguredFlag = 1u << 2;
    private const uint MarkerConfiguredFlag = 1u << 3;
    private NativeManifestFetchAppConfigPreflightResult(NativeManifestFetchAppConfigPreflightStatus status, NativeManifestLoadStatus manifestLoadStatus, NativeManifestParseStatus manifestParseStatus, FetchAppHttpCompletionExpectationStatus expectationStatus, ushort expectedHttpStatusCode, int expectedMarkerLength, int markerBytesWritten, int markerRequiredBytes, uint manifestBytesRead, uint flags)
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

    public NativeManifestFetchAppConfigPreflightStatus Status
    {
        get;
    }
    public NativeManifestLoadStatus ManifestLoadStatus
    {
        get;
    }
    public NativeManifestParseStatus ManifestParseStatus
    {
        get;
    }
    public FetchAppHttpCompletionExpectationStatus ExpectationStatus
    {
        get;
    }
    public ushort ExpectedHttpStatusCode
    {
        get;
    }
    public int ExpectedMarkerLength
    {
        get;
    }
    public int MarkerBytesWritten
    {
        get;
    }
    public int MarkerRequiredBytes
    {
        get;
    }
    public uint ManifestBytesRead
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool UsesDefaultMarker => (Flags & UsesDefaultMarkerFlag) != 0;
    public bool HasConfiguredStatus => (Flags & StatusConfiguredFlag) != 0;
    public bool HasConfiguredMarker => (Flags & MarkerConfiguredFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeManifestFetchAppConfigPreflightStatus.Success && ExpectationStatus == FetchAppHttpCompletionExpectationStatus.Success && ExpectedHttpStatusCode >= FetchAppHttpCompletionExpectation.MinimumHttpStatusCode && ExpectedHttpStatusCode <= FetchAppHttpCompletionExpectation.MaximumHttpStatusCode && ExpectedMarkerLength > 0 && MarkerBytesWritten == ExpectedMarkerLength && MarkerRequiredBytes == ExpectedMarkerLength;

    public static NativeManifestFetchAppConfigPreflightResult Create(NativeManifestFetchAppConfigPreflightStatus status, NativeManifestLoadStatus manifestLoadStatus, NativeManifestParseStatus manifestParseStatus, FetchAppHttpCompletionExpectationStatus expectationStatus, ushort expectedHttpStatusCode, int expectedMarkerLength, int markerBytesWritten, int markerRequiredBytes, uint manifestBytesRead, bool usesDefaultMarker, bool hasConfiguredStatus, bool hasConfiguredMarker)
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

        return new NativeManifestFetchAppConfigPreflightResult(status, manifestLoadStatus, manifestParseStatus, expectationStatus, expectedHttpStatusCode, expectedMarkerLength, markerBytesWritten, markerRequiredBytes, manifestBytesRead, flags);
    }
}
