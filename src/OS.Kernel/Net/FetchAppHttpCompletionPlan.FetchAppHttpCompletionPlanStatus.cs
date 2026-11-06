namespace OS.Net;

public enum FetchAppHttpCompletionPlanStatus
{
    Success = 0,
    HttpPlanFailed = 1,
    UnexpectedHttpStatus = 2,
    InvalidBodyRange = 3,
    MissingExpectedMarker = 4,
    InvalidExpectedMarker = 5,
    ResponseBufferTooSmall = 6
}
