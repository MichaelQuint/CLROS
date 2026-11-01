namespace OS.Net;
using OS.Std.Binary;

public enum DnsResolverTransactionPlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidServerIdentity = 2,
    InvalidQueryName = 3,
    QueryFrameFailed = 4,
    UdpReceiveFailed = 5,
    WrongServer = 6,
    WrongLocalAddress = 7,
    WrongUdpPorts = 8,
    WrongTransaction = 9,
    Nxdomain = 10,
    NoAnswer = 11,
    UnsupportedAnswerType = 12,
    MalformedResponse = 13,
    ResolverParseFailed = 14,
    InvalidState = 15
}
