namespace OS.Net;

public enum TcpConnectionTableStatus
{
    Success = 0,
    InvalidEndpoint = 1,
    InvalidSequenceMetadata = 2,
    InvalidEntryState = 3,
    DuplicateEndpoint = 4,
    TableFull = 5,
    NotFound = 6,
    InvalidLifecycleState = 7
}
