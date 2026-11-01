namespace OS.Net;
public enum TcpClientHandshakeState
{
    Initial = 0,
    SynSent = 1,
    Established = 2,
    Failed = 3
}
