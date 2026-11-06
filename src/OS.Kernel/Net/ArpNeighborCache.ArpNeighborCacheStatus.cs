namespace OS.Net;

public enum ArpNeighborCacheStatus
{
    Success = 0,
    InvalidAddress = 1,
    InvalidMacAddress = 2,
    NotFound = 3,
    CapacityExceeded = 4,
    InvalidTtl = 5,
    InvalidTick = 6
}
