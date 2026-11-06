using OS.Manifest;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Net;

public enum FetchAppHttpRequestEndpointStatus
{
    Success = 0,
    ManifestNotReady = 1,
    UnsupportedScheme = 2,
    MissingHost = 3,
    InvalidPort = 4,
    EmptyPath = 5,
    FieldTooLarge = 6,
    DestinationTooSmall = 7,
    InvalidUrl = 8
}
