namespace OS.Pal;
public sealed partial class OSHostSocketTable
{
    private readonly struct PendingAcceptedConnection
    {
        public readonly OSHostIpv4Endpoint LocalEndpoint;
        public readonly OSHostIpv4Endpoint RemoteEndpoint;
        public PendingAcceptedConnection(OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
        {
            LocalEndpoint = localEndpoint;
            RemoteEndpoint = remoteEndpoint;
        }
    }
}
