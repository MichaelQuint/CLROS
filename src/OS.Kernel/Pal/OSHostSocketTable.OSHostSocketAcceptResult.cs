namespace OS.Pal;
public readonly struct OSHostSocketAcceptResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostSocketHandle Socket;
    public readonly OSHostIpv4Endpoint LocalEndpoint;
    public readonly OSHostIpv4Endpoint RemoteEndpoint;
    public OSHostSocketAcceptResult(OSHostStatus status, OSHostSocketHandle socket, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
    {
        Status = status;
        Socket = socket;
        LocalEndpoint = localEndpoint;
        RemoteEndpoint = remoteEndpoint;
    }

    public bool Succeeded => Status == OSHostStatus.Ok && Socket.IsValid && LocalEndpoint.IsValid && RemoteEndpoint.IsValid;

    public static OSHostSocketAcceptResult FromStatus(OSHostStatus status)
    {
        return new OSHostSocketAcceptResult(status, default, default, default);
    }
}
