using System.Runtime.CompilerServices;
using OS.Net;
using OS.Pal;

internal static class TcpListenerBacklogSocketBridgeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp.listener.socketbridge enqueues accepted connection", TestEnqueuesAcceptedConnection, ref failures);
        Run("net.tcp.listener.socketbridge rejects empty backlog", TestRejectsEmptyBacklog, ref failures);
        Run("net.tcp.listener.socketbridge propagates non-listener failure", TestPropagatesNonListenerFailure, ref failures);
        Run("net.tcp.listener.socketbridge preserves accepted endpoint metadata", TestPreservesAcceptedEndpointMetadata, ref failures);
        Run("net.tcp.listener.socketbridge default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.tcp.listener.socketbridge preserves mock-only boundary", TestPreservesMockOnlyBoundary, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP listener backlog socket bridge host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestEnqueuesAcceptedConnection()
    {
        TcpListenerBacklogEntry[] backlog = new TcpListenerBacklogEntry[2];
        TcpListenerBacklogConnection connection = Connection(remotePort: 50100);
        TcpListenerBacklog.Enqueue(backlog, connection);

        OSHostSocketTable table = new(capacity: 3);
        OSHostSocketHandle listener = CreateListeningSocket(table, Endpoint(0, 0, 0, 0, 8080), backlog: 2);

        TcpListenerBacklogSocketBridgeStatus status = TcpListenerBacklogSocketBridge.EnqueueOldestAcceptedConnection(
            backlog,
            table,
            listener,
            out TcpListenerBacklogSocketBridgeResult result);
        OSHostSocketAcceptResult accepted = table.Accept(listener);

        return status == TcpListenerBacklogSocketBridgeStatus.Success
            && result.Succeeded
            && result.BacklogStatus == TcpListenerBacklogStatus.Success
            && result.SocketStatus == OSHostStatus.Ok
            && result.BacklogCount == 0
            && TcpListenerBacklog.Inspect(backlog).Count == 0
            && accepted.Succeeded
            && accepted.LocalEndpoint.PortHostOrder == connection.LocalPort
            && accepted.RemoteEndpoint.PortHostOrder == connection.RemotePort;
    }

    private static bool TestRejectsEmptyBacklog()
    {
        TcpListenerBacklogEntry[] backlog = new TcpListenerBacklogEntry[1];
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketHandle listener = CreateListeningSocket(table, Endpoint(0, 0, 0, 0, 8080), backlog: 1);

        TcpListenerBacklogSocketBridgeStatus status = TcpListenerBacklogSocketBridge.EnqueueOldestAcceptedConnection(
            backlog,
            table,
            listener,
            out TcpListenerBacklogSocketBridgeResult result);

        return status == TcpListenerBacklogSocketBridgeStatus.BacklogUnavailable
            && !result.Succeeded
            && result.BacklogStatus == TcpListenerBacklogStatus.Success
            && result.SocketStatus == OSHostStatus.WouldBlock
            && result.BacklogCount == 0
            && table.Accept(listener).Status == OSHostStatus.WouldBlock;
    }

    private static bool TestPropagatesNonListenerFailure()
    {
        TcpListenerBacklogEntry[] backlog = new TcpListenerBacklogEntry[1];
        TcpListenerBacklogConnection connection = Connection(remotePort: 50101);
        TcpListenerBacklog.Enqueue(backlog, connection);
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketCreateResult created = table.Create(TcpCreateOptions());

        TcpListenerBacklogSocketBridgeStatus status = TcpListenerBacklogSocketBridge.EnqueueOldestAcceptedConnection(
            backlog,
            table,
            created.Socket,
            out TcpListenerBacklogSocketBridgeResult result);

        return status == TcpListenerBacklogSocketBridgeStatus.SocketEnqueueFailed
            && !result.Succeeded
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && result.BacklogStatus == TcpListenerBacklogStatus.Success
            && TcpListenerBacklog.Inspect(backlog).Count == 1
            && table.Accept(created.Socket).Status == OSHostStatus.InvalidArgument;
    }

    private static bool TestPreservesAcceptedEndpointMetadata()
    {
        TcpListenerBacklogEntry[] backlog = new TcpListenerBacklogEntry[1];
        TcpListenerBacklogConnection connection = new(
            new Ipv4Address(192, 0, 2, 55),
            localPort: 9090,
            new Ipv4Address(198, 51, 100, 77),
            remotePort: 52000,
            localNextSequenceNumber: 0x11112222,
            remoteNextSequenceNumber: 0x33334444);
        TcpListenerBacklog.Enqueue(backlog, connection);

        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketHandle listener = CreateListeningSocket(table, Endpoint(0, 0, 0, 0, 9090), backlog: 1);

        TcpListenerBacklogSocketBridge.EnqueueOldestAcceptedConnection(
            backlog,
            table,
            listener,
            out TcpListenerBacklogSocketBridgeResult bridge);
        OSHostSocketAcceptResult accepted = table.Accept(listener);

        return bridge.Succeeded
            && bridge.LocalEndpoint.Address0 == 192
            && bridge.LocalEndpoint.Address1 == 0
            && bridge.LocalEndpoint.Address2 == 2
            && bridge.LocalEndpoint.Address3 == 55
            && bridge.RemoteEndpoint.Address0 == 198
            && bridge.RemoteEndpoint.Address1 == 51
            && bridge.RemoteEndpoint.Address2 == 100
            && bridge.RemoteEndpoint.Address3 == 77
            && accepted.Succeeded
            && accepted.LocalEndpoint.Address0 == 192
            && accepted.LocalEndpoint.Address3 == 55
            && accepted.RemoteEndpoint.Address0 == 198
            && accepted.RemoteEndpoint.Address3 == 77
            && bridge.Connection.LocalNextSequenceNumber == connection.LocalNextSequenceNumber
            && bridge.Connection.RemoteNextSequenceNumber == connection.RemoteNextSequenceNumber;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpListenerBacklogSocketBridgeResult defaultResult = default;
        TcpListenerBacklogConnection connection = Connection(remotePort: 50103);
        OSHostIpv4Endpoint localEndpoint = Endpoint(192, 0, 2, 10, 8080);
        OSHostIpv4Endpoint remoteEndpoint = Endpoint(198, 51, 100, 183, 50103);

        TcpListenerBacklogSocketBridgeResult failure = TcpListenerBacklogSocketBridgeResult.Create(
            TcpListenerBacklogSocketBridgeStatus.SocketEnqueueFailed,
            TcpListenerBacklogStatus.Success,
            OSHostStatus.InvalidArgument,
            backlogCount: 1,
            backlogCapacity: 2,
            backlogEntryIndex: 0,
            connection,
            localEndpoint,
            remoteEndpoint);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpListenerBacklogSocketBridgeStatus.Success
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == TcpListenerBacklogSocketBridgeStatus.SocketEnqueueFailed
            && failure.BacklogStatus == TcpListenerBacklogStatus.Success
            && failure.SocketStatus == OSHostStatus.InvalidArgument
            && failure.BacklogCount == 1
            && failure.BacklogCapacity == 2
            && failure.BacklogEntryIndex == 0
            && failure.Connection.RemotePort == connection.RemotePort
            && failure.LocalEndpoint.PortHostOrder == localEndpoint.PortHostOrder
            && failure.RemoteEndpoint.PortHostOrder == remoteEndpoint.PortHostOrder
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpListenerBacklogSocketBridgeResult>();
    }

    private static bool TestPreservesMockOnlyBoundary()
    {
        TcpListenerBacklogEntry[] backlog = new TcpListenerBacklogEntry[1];
        TcpListenerBacklog.Enqueue(backlog, Connection(remotePort: 50102));

        TcpListenerBacklogSocketBridgeStatus missingTable = TcpListenerBacklogSocketBridge.EnqueueOldestAcceptedConnection(
            backlog,
            table: null,
            listener: new OSHostSocketHandle(1),
            out TcpListenerBacklogSocketBridgeResult result);

        return TcpListenerBacklogSocketBridge.UsesCallerOwnedBacklog
            && TcpListenerBacklogSocketBridge.UsesMockSocketTable
            && !TcpListenerBacklogSocketBridge.ExportsPalFunctions
            && !TcpListenerBacklogSocketBridge.OpensLiveSockets
            && !TcpListenerBacklogSocketBridge.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpListenerBacklogSocketBridgeResult>()
            && missingTable == TcpListenerBacklogSocketBridgeStatus.MissingSocketTable
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && TcpListenerBacklog.Inspect(backlog).Count == 1;
    }

    private static TcpListenerBacklogConnection Connection(ushort remotePort)
    {
        return new TcpListenerBacklogConnection(
            new Ipv4Address(192, 0, 2, 10),
            localPort: 8080,
            new Ipv4Address(198, 51, 100, (byte)(remotePort & 0xFF)),
            remotePort,
            localNextSequenceNumber: 0xA0B0C0D1,
            remoteNextSequenceNumber: 0x10203041);
    }

    private static OSHostSocketHandle CreateListeningSocket(
        OSHostSocketTable table,
        OSHostIpv4Endpoint endpoint,
        int backlog)
    {
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        if (!create.Succeeded
            || table.Bind(create.Socket, endpoint) != OSHostStatus.Ok
            || table.Listen(create.Socket, backlog) != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Failed to create listening socket fixture.");
        }

        return create.Socket;
    }

    private static OSHostSocketCreateOptions TcpCreateOptions()
    {
        return new OSHostSocketCreateOptions(
            OSHostSocketConstants.DomainIpv4,
            OSHostSocketConstants.TypeStream,
            OSHostSocketConstants.ProtocolTcp,
            flags: 0,
            OSHostSocketTable.CreateOptionsVersion,
            reserved: 0);
    }

    private static OSHostIpv4Endpoint Endpoint(
        byte address0,
        byte address1,
        byte address2,
        byte address3,
        ushort port)
    {
        OSHostStatus status = OSHostIpv4Endpoint.TryCreate(
            address0,
            address1,
            address2,
            address3,
            port,
            out OSHostIpv4Endpoint endpoint);
        if (status != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Invalid endpoint fixture.");
        }

        return endpoint;
    }
}
