namespace OS.Net;
using OS.Std.Memory;

public enum HttpRequestBuilderStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    EmptyHost = 2,
    InvalidHost = 3,
    EmptyPath = 4,
    MissingLeadingSlash = 5,
    InvalidPath = 6
}
