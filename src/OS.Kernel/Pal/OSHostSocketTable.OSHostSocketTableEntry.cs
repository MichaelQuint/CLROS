namespace OS.Pal;

public readonly struct OSHostSocketTableEntry
{
    public readonly OSHostSocketHandle Socket;
    public readonly OSHostSocketLifecycleState State;
    public readonly OSHostSocketCreateOptions CreateOptions;
    public readonly OSHostIpv4Endpoint LocalEndpoint;
    public readonly OSHostIpv4Endpoint RemoteEndpoint;
    public readonly OSHostSocketOptionValue NoDelay;
    public readonly OSHostSocketOptionValue ReuseAddress;
    public readonly OSHostSocketOptionValue ReceiveTimeoutMilliseconds;
    public readonly OSHostSocketOptionValue SendTimeoutMilliseconds;
    public readonly OSHostSocketOptionValue KeepAlive;
    public readonly int ListenBacklog;
    public readonly int PendingAcceptCount;
    public OSHostSocketTableEntry(OSHostSocketHandle socket, OSHostSocketLifecycleState state, OSHostSocketCreateOptions createOptions, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint, OSHostSocketOptionValue noDelay, OSHostSocketOptionValue reuseAddress, OSHostSocketOptionValue receiveTimeoutMilliseconds, OSHostSocketOptionValue sendTimeoutMilliseconds, OSHostSocketOptionValue keepAlive, int listenBacklog, int pendingAcceptCount)
    {
        Socket = socket;
        State = state;
        CreateOptions = createOptions;
        LocalEndpoint = localEndpoint;
        RemoteEndpoint = remoteEndpoint;
        NoDelay = noDelay;
        ReuseAddress = reuseAddress;
        ReceiveTimeoutMilliseconds = receiveTimeoutMilliseconds;
        SendTimeoutMilliseconds = sendTimeoutMilliseconds;
        KeepAlive = keepAlive;
        ListenBacklog = listenBacklog;
        PendingAcceptCount = pendingAcceptCount;
    }
}
