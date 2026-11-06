using OS.Net;

namespace OS.Manifest;

public static class NativeManifestFetchAppConfigPreflight
{
    public static NativeManifestFetchAppConfigPreflightResult Validate(in NativeManifestLoadResult manifestLoad, ReadOnlySpan<byte> manifestBytes, Span<byte> expectedMarkerScratch, bool requireConfiguredMarker = false)
    {
        if (!manifestLoad.Succeeded)
        {
            return NativeManifestFetchAppConfigPreflightResult.Create(NativeManifestFetchAppConfigPreflightStatus.ManifestNotReady, manifestLoad.Status, manifestLoad.ManifestStatus, FetchAppHttpCompletionExpectationStatus.ManifestNotReady, FetchAppHttpCompletionExpectation.DefaultExpectedHttpStatusCode, 0, markerBytesWritten: 0, markerRequiredBytes: 0, manifestLoad.BytesRead, usesDefaultMarker: false, hasConfiguredStatus: false, hasConfiguredMarker: false);
        }

        NativeManifestParseResult manifestParse = NativeManifestParseResult.Success(manifestLoad.Manifest);
        FetchAppHttpCompletionExpectationResult expectation = FetchAppHttpCompletionExpectation.FromManifest(manifestParse, manifestBytes, requireConfiguredMarker);
        if (!expectation.Succeeded)
        {
            return NativeManifestFetchAppConfigPreflightResult.Create(MapExpectationStatus(expectation.Status), manifestLoad.Status, manifestLoad.ManifestStatus, expectation.Status, expectation.ExpectedHttpStatusCode, expectation.ExpectedMarkerLength, markerBytesWritten: 0, markerRequiredBytes: expectation.ExpectedMarkerLength, manifestLoad.BytesRead, expectation.UsesDefaultMarker, expectation.HasConfiguredStatus, expectation.HasConfiguredMarker);
        }

        FetchAppHttpCompletionExpectedMarkerCopyResult markerCopy = FetchAppHttpCompletionExpectation.CopyExpectedMarker(expectation, manifestBytes, expectedMarkerScratch);
        if (!markerCopy.Succeeded)
        {
            return NativeManifestFetchAppConfigPreflightResult.Create(MapExpectationStatus(markerCopy.Status), manifestLoad.Status, manifestLoad.ManifestStatus, markerCopy.Status, expectation.ExpectedHttpStatusCode, expectation.ExpectedMarkerLength, markerCopy.BytesWritten, markerCopy.RequiredBytes, manifestLoad.BytesRead, expectation.UsesDefaultMarker, expectation.HasConfiguredStatus, expectation.HasConfiguredMarker);
        }

        return NativeManifestFetchAppConfigPreflightResult.Create(NativeManifestFetchAppConfigPreflightStatus.Success, manifestLoad.Status, manifestLoad.ManifestStatus, expectation.Status, expectation.ExpectedHttpStatusCode, expectation.ExpectedMarkerLength, markerCopy.BytesWritten, markerCopy.RequiredBytes, manifestLoad.BytesRead, expectation.UsesDefaultMarker, expectation.HasConfiguredStatus, expectation.HasConfiguredMarker);
    }

    private static NativeManifestFetchAppConfigPreflightStatus MapExpectationStatus(FetchAppHttpCompletionExpectationStatus status)
    {
        return status switch
        {
            FetchAppHttpCompletionExpectationStatus.Success => NativeManifestFetchAppConfigPreflightStatus.Success,
            FetchAppHttpCompletionExpectationStatus.ManifestNotReady => NativeManifestFetchAppConfigPreflightStatus.ManifestNotReady,
            FetchAppHttpCompletionExpectationStatus.InvalidStatusCode => NativeManifestFetchAppConfigPreflightStatus.InvalidStatusCode,
            FetchAppHttpCompletionExpectationStatus.EmptyMarker => NativeManifestFetchAppConfigPreflightStatus.EmptyMarker,
            FetchAppHttpCompletionExpectationStatus.MarkerTooLarge => NativeManifestFetchAppConfigPreflightStatus.MarkerTooLarge,
            FetchAppHttpCompletionExpectationStatus.MissingRequiredConfig => NativeManifestFetchAppConfigPreflightStatus.MissingRequiredConfig,
            FetchAppHttpCompletionExpectationStatus.DestinationTooSmall => NativeManifestFetchAppConfigPreflightStatus.DestinationTooSmall,
            _ => NativeManifestFetchAppConfigPreflightStatus.ExpectedMarkerUnavailable
        };
    }
}
