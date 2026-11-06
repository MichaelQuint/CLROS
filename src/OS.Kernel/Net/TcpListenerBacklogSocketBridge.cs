using OS.Pal;

namespace OS.Net;

public static class TcpListenerBacklogSocketBridge
{
    public const bool UsesCallerOwnedBacklog = true;
    public const bool UsesMockSocketTable = true;
    public const bool ExportsPalFunctions = false;
    public const bool OpensLiveSockets = false;
    public const bool StartsKestrel = false;
    public static TcpListenerBacklogSocketBridgeStatus EnqueueOldestAcceptedConnection(Span<TcpListenerBacklogEntry> backlog, OSHostSocketTable? table, OSHostSocketHandle listener, out TcpListenerBacklogSocketBridgeResult result)
    {
        TcpListenerBacklogResult inspected = TcpListenerBacklog.Inspect(backlog);
        if (!inspected.Succeeded || inspected.Count == 0)
        {
            result = CreateResult(TcpListenerBacklogSocketBridgeStatus.BacklogUnavailable, inspected, OSHostStatus.WouldBlock, default, default);
            return result.Status;
        }

        TcpListenerBacklogConnection connection = inspected.Connection;
        OSHostIpv4Endpoint localEndpoint = default;
        OSHostIpv4Endpoint remoteEndpoint = default;
        bool localEndpointCreated = TryCreateEndpoint(connection.LocalAddress, connection.LocalPort, out localEndpoint);
        bool remoteEndpointCreated = TryCreateEndpoint(connection.RemoteAddress, connection.RemotePort, out remoteEndpoint);
        if (!localEndpointCreated || !remoteEndpointCreated)
        {
            result = CreateResult(TcpListenerBacklogSocketBridgeStatus.InvalidEndpoint, inspected, OSHostStatus.InvalidArgument, localEndpoint, remoteEndpoint);
            return result.Status;
        }

        if (table is null)
        {
            result = CreateResult(TcpListenerBacklogSocketBridgeStatus.MissingSocketTable, inspected, OSHostStatus.InvalidArgument, localEndpoint, remoteEndpoint);
            return result.Status;
        }

        OSHostStatus enqueueStatus = table.EnqueueAcceptedConnection(listener, localEndpoint, remoteEndpoint);
        if (enqueueStatus != OSHostStatus.Ok)
        {
            result = CreateResult(TcpListenerBacklogSocketBridgeStatus.SocketEnqueueFailed, inspected, enqueueStatus, localEndpoint, remoteEndpoint);
            return result.Status;
        }

        TcpListenerBacklogResult accepted = TcpListenerBacklog.Accept(backlog);
        if (!accepted.Succeeded)
        {
            result = CreateResult(TcpListenerBacklogSocketBridgeStatus.BacklogAcceptFailed, accepted, OSHostStatus.Ok, localEndpoint, remoteEndpoint);
            return result.Status;
        }

        result = CreateResult(TcpListenerBacklogSocketBridgeStatus.Success, accepted, enqueueStatus, localEndpoint, remoteEndpoint);
        return result.Status;
    }

    private static TcpListenerBacklogSocketBridgeResult CreateResult(TcpListenerBacklogSocketBridgeStatus status, TcpListenerBacklogResult backlog, OSHostStatus socketStatus, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
    {
        return TcpListenerBacklogSocketBridgeResult.Create(status, backlog.Status, socketStatus, backlog.Count, backlog.Capacity, backlog.EntryIndex, backlog.Connection, localEndpoint, remoteEndpoint);
    }

    private static bool TryCreateEndpoint(Ipv4Address address, ushort port, out OSHostIpv4Endpoint endpoint)
    {
        return OSHostIpv4Endpoint.TryCreate(address.Octet0, address.Octet1, address.Octet2, address.Octet3, port, out endpoint) == OSHostStatus.Ok;
    }
}
