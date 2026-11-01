namespace OS.Net;
using OS.Std.Binary;
using OS.Std.Net;

public enum Ipv4TransportChecksumStatus
{
    Success = 0,
    InvalidProtocol = 1,
    InvalidLength = 2
}
