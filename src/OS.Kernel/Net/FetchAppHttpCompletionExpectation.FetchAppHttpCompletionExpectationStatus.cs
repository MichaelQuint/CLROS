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
