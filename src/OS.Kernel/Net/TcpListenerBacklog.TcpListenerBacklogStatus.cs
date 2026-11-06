namespace OS.Net;

public enum TcpListenerBacklogStatus
{
    Success = 0,
    InvalidEndpoint = 1,
    InvalidEntryState = 2,
    DuplicateEndpoint = 3,
    BacklogFull = 4,
    BacklogEmpty = 5,
    EnqueueOrderExhausted = 6
}
