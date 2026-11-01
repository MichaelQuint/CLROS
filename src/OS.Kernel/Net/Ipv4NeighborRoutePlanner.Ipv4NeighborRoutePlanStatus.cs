namespace OS.Net;
public enum Ipv4NeighborRoutePlanStatus
{
    Success = 0,
    InvalidTargetAddress = 1,
    InvalidLocalAddress = 2,
    InvalidLocalMacAddress = 3,
    RequestBufferTooSmall = 4,
    RequestWriteFailed = 5
}
