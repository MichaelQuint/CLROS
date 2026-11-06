namespace OS.Net;

public enum TcpPassiveOpenHandshakeState
{
    Listen = 0,
    SynReceived = 1,
    Established = 2,
    Failed = 3
}
