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
