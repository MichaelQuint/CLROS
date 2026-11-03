using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;

public readonly struct FetchAppHttpCompletionExpectationResult
{
    private const uint InitializedFlag = 1;
    private const uint UsesDefaultMarkerFlag = 1u << 1;
    private const uint StatusConfiguredFlag = 1u << 2;
    private const uint MarkerConfiguredFlag = 1u << 3;
    private FetchAppHttpCompletionExpectationResult(FetchAppHttpCompletionExpectationStatus status, ushort expectedHttpStatusCode, NativeManifestField markerField, int expectedMarkerLength, uint flags)
    {
        Status = status;
        ExpectedHttpStatusCode = expectedHttpStatusCode;
        MarkerField = markerField;
        ExpectedMarkerLength = expectedMarkerLength;
        Flags = flags;
    }

    public FetchAppHttpCompletionExpectationStatus Status
    {
        get;
    }
    public ushort ExpectedHttpStatusCode
    {
        get;
    }
    public NativeManifestField MarkerField
    {
        get;
    }
    public int ExpectedMarkerLength
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
    public bool Succeeded => IsInitialized && Status == FetchAppHttpCompletionExpectationStatus.Success && ExpectedHttpStatusCode >= FetchAppHttpCompletionExpectation.MinimumHttpStatusCode && ExpectedHttpStatusCode <= FetchAppHttpCompletionExpectation.MaximumHttpStatusCode && ExpectedMarkerLength > 0 && ExpectedMarkerLength <= FetchAppHttpCompletionExpectation.MaximumExpectedMarkerLength;

    public static FetchAppHttpCompletionExpectationResult Success(ushort expectedHttpStatusCode, NativeManifestField markerField, int expectedMarkerLength, bool usesDefaultMarker, bool hasConfiguredStatus, bool hasConfiguredMarker)
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

        return new FetchAppHttpCompletionExpectationResult(FetchAppHttpCompletionExpectationStatus.Success, expectedHttpStatusCode, markerField, expectedMarkerLength, flags);
    }

    public static FetchAppHttpCompletionExpectationResult Fail(FetchAppHttpCompletionExpectationStatus status, ushort expectedHttpStatusCode, NativeManifestField markerField, int expectedMarkerLength, bool usesDefaultMarker, bool hasConfiguredStatus, bool hasConfiguredMarker)
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

        return new FetchAppHttpCompletionExpectationResult(status, expectedHttpStatusCode, markerField, expectedMarkerLength, flags);
    }
}
