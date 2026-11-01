namespace OS.Net;
public enum HttpStaticTcpResponsePlanStatus
{
    Success = 0,
    TcpDispatchNotInitialized = 1,
    TcpDispatchFailed = 2,
    NonPayloadDispatch = 3,
    InvalidPayloadRange = 4,
    HttpTransactionFailed = 5
}
