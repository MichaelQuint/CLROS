namespace OS.Net;

public enum FetchAppHttpTcpExchangePlanStatus
{
    Success = 0,
    ContextFailed = 1,
    EndpointBufferTooSmall = 2,
    SynFrameFailed = 3,
    SynAckFrameFailed = 4,
    FinalAckFrameFailed = 5,
    RequestFrameFailed = 6,
    ResponseFrameFailed = 7,
    CompletionFailed = 8,
    CloseFrameFailed = 9
}
