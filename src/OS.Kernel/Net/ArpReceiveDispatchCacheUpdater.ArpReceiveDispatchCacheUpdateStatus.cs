namespace OS.Net;

public enum ArpReceiveDispatchCacheUpdateStatus
{
    Success = 0,
    DispatchFailed = 1,
    NonArpDispatch = 2,
    PayloadRangeInvalid = 3,
    ArpValidationFailed = 4,
    CacheUpdateFailed = 5
}
