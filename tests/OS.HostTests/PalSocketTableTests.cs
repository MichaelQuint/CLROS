using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalSocketTableTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.socket table creates binds and listens", TestCreatesBindsAndListens, ref failures);
        Run("pal.socket table rejects invalid lifecycle", TestRejectsInvalidLifecycle, ref failures);
        Run("pal.socket table reports deterministic exhaustion", TestReportsDeterministicExhaustion, ref failures);
        Run("pal.socket table create default result is not success", TestCreateDefaultResultIsNotSuccess, ref failures);
        Run("pal.socket accept queues connection", TestAcceptQueuesConnection, ref failures);
        Run("pal.socket accept rejects non-listener", TestAcceptRejectsNonListener, ref failures);
        Run("pal.socket accept reports empty queue", TestAcceptReportsEmptyQueue, ref failures);
        Run("pal.socket accept default result is not success", TestAcceptDefaultResultIsNotSuccess, ref failures);
        Run("pal.socket connect establishes outbound endpoint", TestConnectEstablishesOutboundEndpoint, ref failures);
        Run("pal.socket connect rejects invalid state", TestConnectRejectsInvalidState, ref failures);
        Run("pal.socket send receive transfers bytes", TestSendReceiveTransfersBytes, ref failures);
        Run("pal.socket partial receive preserves unread bytes", TestPartialReceivePreservesUnreadBytes, ref failures);
        Run("pal.socket partial send reports backpressure", TestPartialSendReportsBackpressure, ref failures);
        Run("pal.socket peer close returns eof after draining", TestPeerCloseReturnsEofAfterDraining, ref failures);
        Run("pal.socket peer reset reports connection reset", TestPeerResetReportsConnectionReset, ref failures);
        Run("pal.socket poll reports close and reset readiness", TestPollReportsCloseAndResetReadiness, ref failures);
        Run("pal.socket poll reports readiness", TestPollReportsReadiness, ref failures);
        Run("pal.socket poll default result is not success", TestPollDefaultResultIsNotSuccess, ref failures);
        Run("pal.socket shutdown unblocks io", TestShutdownUnblocksIo, ref failures);
        Run("pal.socket send rejects invalid state", TestSendRejectsInvalidState, ref failures);
        Run("pal.socket options store kestrel flags", TestOptionsStoreKestrelFlags, ref failures);
        Run("pal.socket endpoints report bound local address", TestEndpointsReportBoundLocalAddress, ref failures);
        Run("pal.socket endpoints report accepted peer address", TestEndpointsReportAcceptedPeerAddress, ref failures);
        Run("pal.socket reuse address policy is deterministic", TestReuseAddressPolicyIsDeterministic, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL socket table host tests failed: " + failures);
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

    private static bool TestCreatesBindsAndListens()
    {
        OSHostSocketTable table = new(capacity: 4);
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        if (!create.Succeeded
            || create.Status != OSHostStatus.Ok
            || create.Socket.Value != 1
            || table.ActiveCount != 1)
        {
            return false;
        }

        if (OSHostIpv4Endpoint.TryCreate(
                127,
                0,
                0,
                1,
                portHostOrder: 8080,
                out OSHostIpv4Endpoint endpoint) != OSHostStatus.Ok)
        {
            return false;
        }

        if (table.Bind(create.Socket, endpoint) != OSHostStatus.Ok
            || table.TryGetEntry(create.Socket, out OSHostSocketTableEntry boundEntry) != OSHostStatus.Ok
            || boundEntry.State != OSHostSocketLifecycleState.Bound
            || boundEntry.LocalEndpoint.PortHostOrder != 8080)
        {
            return false;
        }

        if (table.Listen(create.Socket, backlog: 16) != OSHostStatus.Ok
            || table.TryGetEntry(create.Socket, out OSHostSocketTableEntry listeningEntry) != OSHostStatus.Ok
            || listeningEntry.State != OSHostSocketLifecycleState.Listening
            || listeningEntry.ListenBacklog != 16)
        {
            return false;
        }

        return table.Close(create.Socket) == OSHostStatus.Ok
            && table.ActiveCount == 0
            && table.TryGetEntry(create.Socket, out _) == OSHostStatus.InvalidArgument;
    }

    private static bool TestRejectsInvalidLifecycle()
    {
        OSHostSocketTable table = new(capacity: 4);

        if (table.Create(TcpCreateOptions(domain: 99)).Status != OSHostStatus.InvalidArgument
            || table.Create(TcpCreateOptions(type: 99)).Status != OSHostStatus.InvalidArgument
            || table.Create(TcpCreateOptions(protocol: 99)).Status != OSHostStatus.InvalidArgument
            || table.Create(TcpCreateOptions(flags: OSHostSocketConstants.CreateFlagNonBlocking << 1)).Status != OSHostStatus.InvalidArgument
            || table.Create(TcpCreateOptions(version: 0)).Status != OSHostStatus.InvalidArgument
            || table.Create(TcpCreateOptions(reserved: 1)).Status != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        OSHostSocketCreateResult first = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult second = table.Create(TcpCreateOptions(flags: OSHostSocketConstants.CreateFlagNonBlocking));
        if (!first.Succeeded || !second.Succeeded)
        {
            return false;
        }

        if (OSHostIpv4Endpoint.TryCreate(
                0,
                0,
                0,
                0,
                portHostOrder: 8080,
                out OSHostIpv4Endpoint any8080) != OSHostStatus.Ok
            || OSHostIpv4Endpoint.TryCreate(
                127,
                0,
                0,
                1,
                portHostOrder: 8080,
                out OSHostIpv4Endpoint loopback8080) != OSHostStatus.Ok
            || OSHostIpv4Endpoint.TryCreate(
                127,
                0,
                0,
                1,
                portHostOrder: 8081,
                out OSHostIpv4Endpoint loopback8081) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketHandle invalidHandle = new(0);
        OSHostSocketHandle missingHandle = new(99);
        if (table.Bind(invalidHandle, any8080) != OSHostStatus.InvalidArgument
            || table.Listen(invalidHandle, backlog: 1) != OSHostStatus.InvalidArgument
            || table.Close(invalidHandle) != OSHostStatus.InvalidArgument
            || table.Bind(missingHandle, any8080) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (table.Listen(first.Socket, backlog: 1) != OSHostStatus.InvalidArgument
            || table.Bind(first.Socket, any8080) != OSHostStatus.Ok
            || table.Bind(first.Socket, loopback8081) != OSHostStatus.InvalidArgument
            || table.Bind(second.Socket, loopback8080) != OSHostStatus.AddressInUse
            || table.Listen(first.Socket, backlog: 0) != OSHostStatus.InvalidArgument
            || table.Listen(first.Socket, OSHostSocketTable.MaxListenBacklog + 1) != OSHostStatus.InvalidArgument
            || table.Listen(first.Socket, backlog: 32) != OSHostStatus.Ok
            || table.Listen(first.Socket, backlog: 32) != OSHostStatus.InvalidArgument
            || table.Bind(first.Socket, loopback8081) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        return table.Close(first.Socket) == OSHostStatus.Ok
            && table.Bind(second.Socket, loopback8080) == OSHostStatus.Ok
            && table.Listen(second.Socket, backlog: 1) == OSHostStatus.Ok
            && table.Close(first.Socket) == OSHostStatus.InvalidArgument;
    }

    private static bool TestReportsDeterministicExhaustion()
    {
        OSHostSocketTable table = new(capacity: 2);

        OSHostSocketCreateResult first = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult second = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult third = table.Create(TcpCreateOptions());
        if (!first.Succeeded
            || !second.Succeeded
            || first.Socket.Value != 1
            || second.Socket.Value != 2
            || third.Status != OSHostStatus.OutOfMemory
            || third.Socket.IsValid
            || table.ActiveCount != 2)
        {
            return false;
        }

        if (table.Close(first.Socket) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketCreateResult reused = table.Create(TcpCreateOptions());
        if (!reused.Succeeded
            || reused.Socket.Value != 1
            || table.ActiveCount != 2)
        {
            return false;
        }

        OSHostSocketTable emptyTable = new(capacity: 0);
        OSHostSocketCreateResult emptyCreate = emptyTable.Create(TcpCreateOptions());
        return emptyTable.Capacity == 0
            && emptyCreate.Status == OSHostStatus.OutOfMemory
            && !emptyCreate.Socket.IsValid;
    }

    private static bool TestCreateDefaultResultIsNotSuccess()
    {
        OSHostSocketCreateResult create = default;

        return create.Status == OSHostStatus.Ok
            && !create.Succeeded
            && !create.Socket.IsValid
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketCreateResult>();
    }

    private static bool TestAcceptQueuesConnection()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateListeningSocket(
                table,
                backlog: 1,
                Endpoint(0, 0, 0, 0, 5000),
                out OSHostSocketHandle listener))
        {
            return false;
        }

        OSHostIpv4Endpoint acceptedLocal = Endpoint(10, 0, 2, 15, 5000);
        OSHostIpv4Endpoint remote = Endpoint(203, 0, 113, 10, 50500);
        OSHostIpv4Endpoint secondRemote = Endpoint(203, 0, 113, 11, 50501);

        if (table.EnqueueAcceptedConnection(listener, acceptedLocal, remote) != OSHostStatus.Ok
            || table.EnqueueAcceptedConnection(listener, acceptedLocal, secondRemote) != OSHostStatus.WouldBlock
            || table.TryGetEntry(listener, out OSHostSocketTableEntry queuedEntry) != OSHostStatus.Ok
            || queuedEntry.PendingAcceptCount != 1)
        {
            return false;
        }

        OSHostSocketAcceptResult accept = table.Accept(listener);
        if (!accept.Succeeded
            || accept.Socket.Value == listener.Value
            || accept.LocalEndpoint.Address3 != 15
            || accept.LocalEndpoint.PortHostOrder != 5000
            || accept.RemoteEndpoint.Address3 != 10
            || accept.RemoteEndpoint.PortHostOrder != 50500
            || table.ActiveCount != 2)
        {
            return false;
        }

        if (table.TryGetEntry(listener, out OSHostSocketTableEntry listenerEntry) != OSHostStatus.Ok
            || listenerEntry.PendingAcceptCount != 0
            || table.TryGetEntry(accept.Socket, out OSHostSocketTableEntry acceptedEntry) != OSHostStatus.Ok
            || acceptedEntry.State != OSHostSocketLifecycleState.Connected
            || acceptedEntry.LocalEndpoint.Address0 != 10
            || acceptedEntry.RemoteEndpoint.Address0 != 203)
        {
            return false;
        }

        return table.Close(accept.Socket) == OSHostStatus.Ok
            && table.ActiveCount == 1
            && table.Close(listener) == OSHostStatus.Ok
            && table.ActiveCount == 0;
    }

    private static bool TestAcceptRejectsNonListener()
    {
        OSHostSocketTable table = new(capacity: 3);
        OSHostSocketCreateResult created = table.Create(TcpCreateOptions());
        if (!created.Succeeded)
        {
            return false;
        }

        OSHostIpv4Endpoint local = Endpoint(127, 0, 0, 1, 8080);
        OSHostIpv4Endpoint remote = Endpoint(127, 0, 0, 1, 50500);
        OSHostSocketHandle invalidHandle = new(0);

        if (table.Accept(invalidHandle).Status != OSHostStatus.InvalidArgument
            || table.EnqueueAcceptedConnection(invalidHandle, local, remote) != OSHostStatus.InvalidArgument
            || table.Accept(created.Socket).Status != OSHostStatus.InvalidArgument
            || table.EnqueueAcceptedConnection(created.Socket, local, remote) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (table.Bind(created.Socket, local) != OSHostStatus.Ok
            || table.Accept(created.Socket).Status != OSHostStatus.InvalidArgument
            || table.EnqueueAcceptedConnection(created.Socket, local, remote) != OSHostStatus.InvalidArgument
            || table.Listen(created.Socket, backlog: 1) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostIpv4Endpoint wrongLocalPort = Endpoint(127, 0, 0, 1, 8081);
        if (table.EnqueueAcceptedConnection(created.Socket, wrongLocalPort, remote) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        return table.Close(created.Socket) == OSHostStatus.Ok
            && table.Accept(created.Socket).Status == OSHostStatus.InvalidArgument
            && table.EnqueueAcceptedConnection(created.Socket, local, remote) == OSHostStatus.InvalidArgument;
    }

    private static bool TestAcceptReportsEmptyQueue()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateListeningSocket(
                table,
                backlog: 2,
                Endpoint(127, 0, 0, 1, 9000),
                out OSHostSocketHandle listener))
        {
            return false;
        }

        OSHostSocketAcceptResult empty = table.Accept(listener);
        if (empty.Status != OSHostStatus.WouldBlock
            || empty.Socket.IsValid
            || table.ActiveCount != 1)
        {
            return false;
        }

        OSHostSocketTable fullTable = new(capacity: 1);
        if (!CreateListeningSocket(
                fullTable,
                backlog: 1,
                Endpoint(127, 0, 0, 1, 9001),
                out OSHostSocketHandle fullListener))
        {
            return false;
        }

        if (fullTable.EnqueueAcceptedConnection(
                fullListener,
                Endpoint(127, 0, 0, 1, 9001),
                Endpoint(127, 0, 0, 1, 50501)) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketAcceptResult noSlot = fullTable.Accept(fullListener);
        return noSlot.Status == OSHostStatus.OutOfMemory
            && !noSlot.Socket.IsValid
            && fullTable.TryGetEntry(fullListener, out OSHostSocketTableEntry fullEntry) == OSHostStatus.Ok
            && fullEntry.PendingAcceptCount == 1;
    }

    private static bool TestAcceptDefaultResultIsNotSuccess()
    {
        OSHostSocketAcceptResult accept = default;

        return accept.Status == OSHostStatus.Ok
            && !accept.Succeeded
            && !accept.Socket.IsValid
            && !accept.LocalEndpoint.IsValid
            && !accept.RemoteEndpoint.IsValid
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketAcceptResult>();
    }

    private static bool TestConnectEstablishesOutboundEndpoint()
    {
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        OSHostIpv4Endpoint local = Endpoint(10, 0, 2, 15, 41000);
        OSHostIpv4Endpoint remote = Endpoint(198, 51, 100, 10, 443);
        if (!create.Succeeded
            || table.Bind(create.Socket, local) != OSHostStatus.Ok
            || table.Connect(create.Socket, remote) != OSHostStatus.Ok
            || table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) != OSHostStatus.Ok)
        {
            return false;
        }

        byte[] payload = [0x48, 0x54, 0x54, 0x50];
        byte[] drained = new byte[4];
        OSHostSocketOperationResult send = table.Send(create.Socket, payload);
        OSHostSocketOperationResult drain = table.DrainOutboundData(create.Socket, drained);

        return entry.State == OSHostSocketLifecycleState.Connected
            && entry.LocalEndpoint.PortHostOrder == 41000
            && entry.RemoteEndpoint.Address0 == 198
            && entry.RemoteEndpoint.PortHostOrder == 443
            && entry.PendingAcceptCount == 0
            && table.GetRemoteEndpoint(create.Socket, out OSHostIpv4Endpoint queriedRemote) == OSHostStatus.Ok
            && queriedRemote.PortHostOrder == 443
            && send.Succeeded
            && send.BytesTransferred == 4
            && drain.Succeeded
            && drained[0] == 0x48
            && drained[3] == 0x50;
    }

    private static bool TestConnectRejectsInvalidState()
    {
        OSHostSocketTable table = new(capacity: 3);
        OSHostSocketCreateResult unbound = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult listener = table.Create(TcpCreateOptions());
        OSHostIpv4Endpoint listenEndpoint = Endpoint(127, 0, 0, 1, 9090);
        OSHostIpv4Endpoint remote = Endpoint(203, 0, 113, 8, 443);
        OSHostIpv4Endpoint anyRemote = Endpoint(0, 0, 0, 0, 443);
        if (!unbound.Succeeded
            || !listener.Succeeded
            || table.Bind(listener.Socket, listenEndpoint) != OSHostStatus.Ok
            || table.Listen(listener.Socket, backlog: 1) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketHandle invalidHandle = new(0);
        OSHostStatus invalidHandleStatus = table.Connect(invalidHandle, remote);
        OSHostStatus unboundStatus = table.Connect(unbound.Socket, remote);
        OSHostStatus listenerStatus = table.Connect(listener.Socket, remote);
        OSHostStatus anyRemoteStatus = table.Connect(unbound.Socket, anyRemote);

        return invalidHandleStatus == OSHostStatus.InvalidArgument
            && unboundStatus == OSHostStatus.AddressNotAvailable
            && listenerStatus == OSHostStatus.InvalidArgument
            && anyRemoteStatus == OSHostStatus.InvalidArgument
            && table.TryGetEntry(unbound.Socket, out OSHostSocketTableEntry unboundEntry) == OSHostStatus.Ok
            && unboundEntry.State == OSHostSocketLifecycleState.Created
            && table.TryGetEntry(listener.Socket, out OSHostSocketTableEntry listenerEntry) == OSHostStatus.Ok
            && listenerEntry.State == OSHostSocketLifecycleState.Listening;
    }

    private static bool TestSendReceiveTransfersBytes()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateAcceptedSocket(table, out _, out OSHostSocketHandle socket))
        {
            return false;
        }

        byte[] inbound = [0x10, 0x20, 0x30, 0x40];
        byte[] receiveBuffer = new byte[4];
        if (table.QueueInboundData(socket, inbound) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketOperationResult receive = table.Receive(socket, receiveBuffer);
        if (!receive.Succeeded
            || receive.BytesTransferred != 4
            || receiveBuffer[0] != 0x10
            || receiveBuffer[1] != 0x20
            || receiveBuffer[2] != 0x30
            || receiveBuffer[3] != 0x40
            || table.Receive(socket, receiveBuffer).StatusCode != (int)OSHostStatus.WouldBlock)
        {
            return false;
        }

        byte[] outbound = [0x41, 0x42, 0x43];
        byte[] drainBuffer = new byte[3];
        OSHostSocketOperationResult send = table.Send(socket, outbound);
        OSHostSocketOperationResult drain = table.DrainOutboundData(socket, drainBuffer);
        return send.Succeeded
            && send.BytesTransferred == 3
            && drain.Succeeded
            && drain.BytesTransferred == 3
            && drainBuffer[0] == 0x41
            && drainBuffer[1] == 0x42
            && drainBuffer[2] == 0x43
            && table.DrainOutboundData(socket, drainBuffer).StatusCode == (int)OSHostStatus.WouldBlock;
    }

    private static bool TestPartialReceivePreservesUnreadBytes()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateAcceptedSocket(table, out _, out OSHostSocketHandle socket)
            || table.QueueInboundData(socket, [0x10, 0x20, 0x30, 0x40, 0x50]) != OSHostStatus.Ok)
        {
            return false;
        }

        byte[] firstBuffer = new byte[2];
        OSHostSocketOperationResult firstReceive = table.Receive(socket, firstBuffer);
        if (!firstReceive.Succeeded
            || firstReceive.BytesTransferred != 2
            || firstBuffer[0] != 0x10
            || firstBuffer[1] != 0x20)
        {
            return false;
        }

        OSHostSocketPollResult afterPartialRead = table.Poll(
            socket,
            OSHostSocketConstants.PollReadable);
        if (!afterPartialRead.Succeeded
            || afterPartialRead.ObservedEvents != OSHostSocketConstants.PollReadable)
        {
            return false;
        }

        byte[] secondBuffer = new byte[3];
        OSHostSocketOperationResult secondReceive = table.Receive(socket, secondBuffer);
        return secondReceive.Succeeded
            && secondReceive.BytesTransferred == 3
            && secondBuffer[0] == 0x30
            && secondBuffer[1] == 0x40
            && secondBuffer[2] == 0x50
            && table.Receive(socket, secondBuffer).StatusCode == (int)OSHostStatus.WouldBlock;
    }

    private static bool TestPartialSendReportsBackpressure()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateAcceptedSocket(table, out _, out OSHostSocketHandle socket))
        {
            return false;
        }

        byte[] oversizedPayload = new byte[OSHostSocketTable.ConnectedBufferCapacity + 3];
        for (int i = 0; i < oversizedPayload.Length; i++)
        {
            oversizedPayload[i] = (byte)(i % 251);
        }

        OSHostSocketOperationResult partialSend = table.Send(socket, oversizedPayload);
        if (!partialSend.Succeeded
            || partialSend.BytesTransferred != OSHostSocketTable.ConnectedBufferCapacity)
        {
            return false;
        }

        OSHostSocketOperationResult blockedSend = table.Send(socket, [0xCC]);
        if (blockedSend.StatusCode != (int)OSHostStatus.WouldBlock
            || blockedSend.BytesTransferred != 0)
        {
            return false;
        }

        byte[] drainBuffer = new byte[OSHostSocketTable.ConnectedBufferCapacity];
        OSHostSocketOperationResult drain = table.DrainOutboundData(socket, drainBuffer);
        if (!drain.Succeeded
            || drain.BytesTransferred != OSHostSocketTable.ConnectedBufferCapacity
            || drainBuffer[0] != 0
            || drainBuffer[1] != 1
            || drainBuffer[^1] != (byte)((OSHostSocketTable.ConnectedBufferCapacity - 1) % 251))
        {
            return false;
        }

        OSHostSocketOperationResult retrySend = table.Send(socket, oversizedPayload.AsSpan(OSHostSocketTable.ConnectedBufferCapacity));
        return retrySend.Succeeded
            && retrySend.BytesTransferred == 3;
    }

    private static bool TestPeerCloseReturnsEofAfterDraining()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateAcceptedSocket(table, out _, out OSHostSocketHandle socket)
            || table.QueueInboundData(socket, [0xA1, 0xA2, 0xA3]) != OSHostStatus.Ok
            || table.MarkPeerClosed(socket) != OSHostStatus.Ok)
        {
            return false;
        }

        byte[] firstBuffer = new byte[2];
        OSHostSocketOperationResult firstReceive = table.Receive(socket, firstBuffer);
        if (!firstReceive.Succeeded
            || firstReceive.BytesTransferred != 2
            || firstBuffer[0] != 0xA1
            || firstBuffer[1] != 0xA2)
        {
            return false;
        }

        byte[] secondBuffer = new byte[2];
        OSHostSocketOperationResult secondReceive = table.Receive(socket, secondBuffer);
        if (!secondReceive.Succeeded
            || secondReceive.BytesTransferred != 1
            || secondBuffer[0] != 0xA3)
        {
            return false;
        }

        OSHostSocketOperationResult eof = table.Receive(socket, secondBuffer);
        return eof.StatusCode == (int)OSHostStatus.EndOfFile
            && eof.BytesTransferred == 0
            && table.Receive(socket, secondBuffer).StatusCode == (int)OSHostStatus.EndOfFile;
    }

    private static bool TestPeerResetReportsConnectionReset()
    {
        OSHostSocketTable table = new(capacity: 4);
        if (!CreateAcceptedSocket(
                table,
                out OSHostSocketHandle listener,
                out OSHostSocketHandle socket))
        {
            return false;
        }

        OSHostSocketCreateResult unrelated = table.Create(TcpCreateOptions());
        if (!unrelated.Succeeded
            || table.QueueInboundData(socket, [0xB1, 0xB2]) != OSHostStatus.Ok
            || table.MarkPeerReset(socket) != OSHostStatus.Ok)
        {
            return false;
        }

        byte[] receiveBuffer = new byte[2];
        OSHostSocketOperationResult receive = table.Receive(socket, receiveBuffer);
        OSHostSocketOperationResult send = table.Send(socket, [0xB3]);
        OSHostSocketPollResult poll = table.Poll(
            socket,
            OSHostSocketConstants.PollReadable
                | OSHostSocketConstants.PollWritable
                | OSHostSocketConstants.PollError
                | OSHostSocketConstants.PollHangup);

        return receive.StatusCode == (int)OSHostStatus.ConnectionReset
            && receive.BytesTransferred == 0
            && send.StatusCode == (int)OSHostStatus.ConnectionReset
            && send.BytesTransferred == 0
            && poll.Status == OSHostStatus.ConnectionReset
            && poll.ObservedEvents == (OSHostSocketConstants.PollError | OSHostSocketConstants.PollHangup)
            && table.ActiveCount == 3
            && table.TryGetEntry(listener, out _) == OSHostStatus.Ok
            && table.TryGetEntry(unrelated.Socket, out _) == OSHostStatus.Ok;
    }

    private static bool TestPollReportsCloseAndResetReadiness()
    {
        OSHostSocketTable closeTable = new(capacity: 2);
        if (!CreateAcceptedSocket(closeTable, out _, out OSHostSocketHandle closeSocket)
            || closeTable.QueueInboundData(closeSocket, [0xC1]) != OSHostStatus.Ok
            || closeTable.MarkPeerClosed(closeSocket) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketPollResult closePoll = closeTable.Poll(
            closeSocket,
            OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup);
        if (!closePoll.Succeeded
            || closePoll.ObservedEvents != (OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup))
        {
            return false;
        }

        byte[] receiveBuffer = new byte[1];
        if (!closeTable.Receive(closeSocket, receiveBuffer).Succeeded)
        {
            return false;
        }

        OSHostSocketPollResult eofPoll = closeTable.Poll(
            closeSocket,
            OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup);
        if (!eofPoll.Succeeded
            || eofPoll.ObservedEvents != (OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup))
        {
            return false;
        }

        OSHostSocketTable resetTable = new(capacity: 2);
        if (!CreateAcceptedSocket(resetTable, out _, out OSHostSocketHandle resetSocket)
            || resetTable.MarkPeerReset(resetSocket) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketPollResult resetPoll = resetTable.Poll(
            resetSocket,
            OSHostSocketConstants.PollReadable
                | OSHostSocketConstants.PollWritable
                | OSHostSocketConstants.PollError
                | OSHostSocketConstants.PollHangup);
        return resetPoll.Status == OSHostStatus.ConnectionReset
            && resetPoll.ObservedEvents == (OSHostSocketConstants.PollError | OSHostSocketConstants.PollHangup);
    }

    private static bool TestPollReportsReadiness()
    {
        OSHostSocketTable table = new(capacity: 3);
        if (!CreateAcceptedSocket(
                table,
                out OSHostSocketHandle listener,
                out OSHostSocketHandle socket))
        {
            return false;
        }

        OSHostSocketPollResult listenerPoll = table.Poll(
            listener,
            OSHostSocketConstants.PollAccept | OSHostSocketConstants.PollReadable);
        if (!listenerPoll.Succeeded || listenerPoll.ObservedEvents != 0)
        {
            return false;
        }

        if (table.EnqueueAcceptedConnection(
                listener,
                Endpoint(127, 0, 0, 1, 7000),
                Endpoint(127, 0, 0, 1, 50501)) != OSHostStatus.Ok)
        {
            return false;
        }

        listenerPoll = table.Poll(
            listener,
            OSHostSocketConstants.PollAccept | OSHostSocketConstants.PollReadable);
        if (!listenerPoll.Succeeded
            || listenerPoll.ObservedEvents != (OSHostSocketConstants.PollAccept | OSHostSocketConstants.PollReadable))
        {
            return false;
        }

        OSHostSocketPollResult writePoll = table.Poll(
            socket,
            OSHostSocketConstants.PollWritable);
        if (!writePoll.Succeeded || writePoll.ObservedEvents != OSHostSocketConstants.PollWritable)
        {
            return false;
        }

        if (table.QueueInboundData(socket, [0x61]) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketPollResult readWritePoll = table.Poll(
            socket,
            OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollWritable);
        return readWritePoll.Succeeded
            && readWritePoll.ObservedEvents == (OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollWritable)
            && table.Poll(new OSHostSocketHandle(0), OSHostSocketConstants.PollReadable).Status == OSHostStatus.InvalidArgument
            && table.Poll(socket, OSHostSocketConstants.PollAccept << 1).Status == OSHostStatus.InvalidArgument;
    }

    private static bool TestPollDefaultResultIsNotSuccess()
    {
        OSHostSocketPollResult result = default;
        OSHostSocketTable table = new(capacity: 3);
        if (!CreateAcceptedSocket(
                table,
                out OSHostSocketHandle listener,
                out _))
        {
            return false;
        }

        OSHostSocketPollResult zeroEventPoll = table.Poll(
            listener,
            OSHostSocketConstants.PollAccept | OSHostSocketConstants.PollReadable);

        return result.Status == OSHostStatus.Ok
            && !result.Succeeded
            && !result.IsInitialized
            && result.ObservedEvents == 0
            && zeroEventPoll.Status == OSHostStatus.Ok
            && zeroEventPoll.Succeeded
            && zeroEventPoll.IsInitialized
            && zeroEventPoll.ObservedEvents == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketPollResult>();
    }

    private static bool TestShutdownUnblocksIo()
    {
        OSHostSocketTable table = new(capacity: 2);
        if (!CreateAcceptedSocket(table, out _, out OSHostSocketHandle socket))
        {
            return false;
        }

        if (table.QueueInboundData(socket, [0x51, 0x52]) != OSHostStatus.Ok
            || table.Shutdown(socket, OSHostSocketConstants.ShutdownSend) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketPollResult sendShutdownPoll = table.Poll(
            socket,
            OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollWritable | OSHostSocketConstants.PollHangup);
        if (!sendShutdownPoll.Succeeded
            || sendShutdownPoll.ObservedEvents != (OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup)
            || table.Send(socket, [0x01]).StatusCode != (int)OSHostStatus.Interrupted)
        {
            return false;
        }

        byte[] receiveBuffer = new byte[2];
        OSHostSocketOperationResult receive = table.Receive(socket, receiveBuffer);
        if (!receive.Succeeded
            || receive.BytesTransferred != 2
            || receiveBuffer[0] != 0x51
            || receiveBuffer[1] != 0x52)
        {
            return false;
        }

        if (table.Shutdown(socket, OSHostSocketConstants.ShutdownReceive) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketPollResult bothShutdownPoll = table.Poll(
            socket,
            OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollWritable | OSHostSocketConstants.PollHangup);
        return bothShutdownPoll.Succeeded
            && bothShutdownPoll.ObservedEvents == OSHostSocketConstants.PollHangup
            && table.Receive(socket, receiveBuffer).StatusCode == (int)OSHostStatus.Interrupted
            && table.QueueInboundData(socket, [0x53]) == OSHostStatus.Interrupted;
    }

    private static bool TestSendRejectsInvalidState()
    {
        OSHostSocketTable table = new(capacity: 3);
        OSHostSocketCreateResult created = table.Create(TcpCreateOptions());
        if (!created.Succeeded)
        {
            return false;
        }

        OSHostSocketHandle invalid = new(0);
        if (table.Send(invalid, [0x01]).StatusCode != (int)OSHostStatus.InvalidArgument
            || table.Send(created.Socket, [0x01]).StatusCode != (int)OSHostStatus.InvalidArgument
            || table.Receive(created.Socket, new byte[1]).StatusCode != (int)OSHostStatus.InvalidArgument
            || table.Shutdown(created.Socket, OSHostSocketConstants.ShutdownBoth) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (table.Bind(created.Socket, Endpoint(127, 0, 0, 1, 7100)) != OSHostStatus.Ok
            || table.Listen(created.Socket, backlog: 1) != OSHostStatus.Ok
            || table.Send(created.Socket, [0x01]).StatusCode != (int)OSHostStatus.InvalidArgument)
        {
            return false;
        }

        if (!CreateAcceptedSocket(table, out _, out OSHostSocketHandle connected))
        {
            return false;
        }

        if (table.QueueInboundData(connected, [0x01]) != OSHostStatus.Ok
            || table.Receive(connected, []).StatusCode != (int)OSHostStatus.BufferTooSmall
            || table.Shutdown(connected, 0) != OSHostStatus.InvalidArgument
            || table.Shutdown(connected, OSHostSocketConstants.ShutdownBoth << 1) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        return table.Close(connected) == OSHostStatus.Ok
            && table.Send(connected, [0x01]).StatusCode == (int)OSHostStatus.InvalidArgument;
    }

    private static bool TestOptionsStoreKestrelFlags()
    {
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketCreateResult created = table.Create(TcpCreateOptions());
        if (!created.Succeeded)
        {
            return false;
        }

        if (table.SetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionNoDelay,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok
            || table.SetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionReuseAddress,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok
            || table.SetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds,
                OSHostSocketOptionValue.TimeoutMilliseconds(2500)) != OSHostStatus.Ok
            || table.SetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds,
                OSHostSocketOptionValue.TimeoutMilliseconds(3500)) != OSHostStatus.Ok
            || table.SetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionKeepAlive,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok)
        {
            return false;
        }

        if (table.GetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionNoDelay,
                out OSHostSocketOptionValue noDelay) != OSHostStatus.Ok
            || table.GetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionReuseAddress,
                out OSHostSocketOptionValue reuseAddress) != OSHostStatus.Ok
            || table.GetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds,
                out OSHostSocketOptionValue receiveTimeout) != OSHostStatus.Ok
            || table.GetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds,
                out OSHostSocketOptionValue sendTimeout) != OSHostStatus.Ok
            || table.GetSocketOption(
                created.Socket,
                OSHostSocketConstants.SocketOptionKeepAlive,
                out OSHostSocketOptionValue keepAlive) != OSHostStatus.Ok)
        {
            return false;
        }

        if (!noDelay.IsBooleanEnabled
            || !reuseAddress.IsBooleanEnabled
            || receiveTimeout.Value != 2500
            || sendTimeout.Value != 3500
            || !keepAlive.IsBooleanEnabled)
        {
            return false;
        }

        return table.TryGetEntry(created.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.NoDelay.IsBooleanEnabled
            && entry.ReuseAddress.IsBooleanEnabled
            && entry.ReceiveTimeoutMilliseconds.Value == 2500
            && entry.SendTimeoutMilliseconds.Value == 3500
            && entry.KeepAlive.IsBooleanEnabled;
    }

    private static bool TestEndpointsReportBoundLocalAddress()
    {
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketCreateResult created = table.Create(TcpCreateOptions());
        OSHostIpv4Endpoint local = Endpoint(10, 1, 2, 3, 8088);
        if (!created.Succeeded
            || table.GetLocalEndpoint(created.Socket, out _) != OSHostStatus.InvalidArgument
            || table.Bind(created.Socket, local) != OSHostStatus.Ok
            || table.GetLocalEndpoint(created.Socket, out OSHostIpv4Endpoint reported) != OSHostStatus.Ok)
        {
            return false;
        }

        return reported.Address0 == 10
            && reported.Address1 == 1
            && reported.Address2 == 2
            && reported.Address3 == 3
            && reported.PortHostOrder == 8088
            && table.GetRemoteEndpoint(created.Socket, out _) == OSHostStatus.InvalidArgument;
    }

    private static bool TestEndpointsReportAcceptedPeerAddress()
    {
        OSHostSocketTable table = new(capacity: 3);
        if (!CreateListeningSocket(
                table,
                backlog: 1,
                Endpoint(0, 0, 0, 0, 8090),
                out OSHostSocketHandle listener))
        {
            return false;
        }

        OSHostIpv4Endpoint local = Endpoint(192, 0, 2, 10, 8090);
        OSHostIpv4Endpoint remote = Endpoint(198, 51, 100, 25, 53000);
        if (table.EnqueueAcceptedConnection(listener, local, remote) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketAcceptResult accept = table.Accept(listener);
        if (!accept.Succeeded
            || table.GetLocalEndpoint(accept.Socket, out OSHostIpv4Endpoint reportedLocal) != OSHostStatus.Ok
            || table.GetRemoteEndpoint(accept.Socket, out OSHostIpv4Endpoint reportedRemote) != OSHostStatus.Ok)
        {
            return false;
        }

        return reportedLocal.Address0 == 192
            && reportedLocal.Address3 == 10
            && reportedLocal.PortHostOrder == 8090
            && reportedRemote.Address0 == 198
            && reportedRemote.Address1 == 51
            && reportedRemote.Address2 == 100
            && reportedRemote.Address3 == 25
            && reportedRemote.PortHostOrder == 53000;
    }

    private static bool TestReuseAddressPolicyIsDeterministic()
    {
        OSHostSocketTable table = new(capacity: 4);
        OSHostIpv4Endpoint any8089 = Endpoint(0, 0, 0, 0, 8089);
        OSHostSocketCreateResult first = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult second = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult third = table.Create(TcpCreateOptions());
        OSHostSocketCreateResult fourth = table.Create(TcpCreateOptions());
        if (!first.Succeeded || !second.Succeeded || !third.Succeeded || !fourth.Succeeded)
        {
            return false;
        }

        if (table.SetSocketOption(
                first.Socket,
                OSHostSocketConstants.SocketOptionReuseAddress,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok
            || table.SetSocketOption(
                second.Socket,
                OSHostSocketConstants.SocketOptionReuseAddress,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok
            || table.SetSocketOption(
                fourth.Socket,
                OSHostSocketConstants.SocketOptionReuseAddress,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok)
        {
            return false;
        }

        if (table.Bind(first.Socket, any8089) != OSHostStatus.Ok
            || table.Bind(second.Socket, any8089) != OSHostStatus.Ok
            || table.Bind(third.Socket, any8089) != OSHostStatus.AddressInUse
            || table.Bind(fourth.Socket, any8089) != OSHostStatus.Ok)
        {
            return false;
        }

        return table.Listen(first.Socket, backlog: 1) == OSHostStatus.Ok
            && table.Listen(second.Socket, backlog: 1) == OSHostStatus.Ok
            && table.Listen(fourth.Socket, backlog: 1) == OSHostStatus.Ok;
    }

    private static OSHostSocketCreateOptions TcpCreateOptions(
        uint domain = OSHostSocketConstants.DomainIpv4,
        uint type = OSHostSocketConstants.TypeStream,
        uint protocol = OSHostSocketConstants.ProtocolTcp,
        uint flags = 0,
        uint version = OSHostSocketTable.CreateOptionsVersion,
        uint reserved = 0)
    {
        return new OSHostSocketCreateOptions(
            domain,
            type,
            protocol,
            flags,
            version,
            reserved);
    }

    private static bool CreateListeningSocket(
        OSHostSocketTable table,
        int backlog,
        OSHostIpv4Endpoint endpoint,
        out OSHostSocketHandle listener)
    {
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        listener = create.Socket;
        return create.Succeeded
            && table.Bind(listener, endpoint) == OSHostStatus.Ok
            && table.Listen(listener, backlog) == OSHostStatus.Ok;
    }

    private static bool CreateAcceptedSocket(
        OSHostSocketTable table,
        out OSHostSocketHandle listener,
        out OSHostSocketHandle accepted)
    {
        listener = default;
        accepted = default;
        if (!CreateListeningSocket(
                table,
                backlog: 2,
                Endpoint(127, 0, 0, 1, 7000),
                out listener))
        {
            return false;
        }

        if (table.EnqueueAcceptedConnection(
                listener,
                Endpoint(127, 0, 0, 1, 7000),
                Endpoint(127, 0, 0, 1, 50500)) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostSocketAcceptResult accept = table.Accept(listener);
        accepted = accept.Socket;
        return accept.Succeeded;
    }

    private static OSHostIpv4Endpoint Endpoint(
        byte address0,
        byte address1,
        byte address2,
        byte address3,
        ushort portHostOrder)
    {
        OSHostStatus status = OSHostIpv4Endpoint.TryCreate(
            address0,
            address1,
            address2,
            address3,
            portHostOrder,
            out OSHostIpv4Endpoint endpoint);
        if (status != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Invalid test endpoint");
        }

        return endpoint;
    }
}
