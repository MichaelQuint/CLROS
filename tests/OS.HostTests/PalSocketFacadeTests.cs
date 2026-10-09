using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalSocketFacadeTests
{

    [HostTest("pal.socket facade creates binds and listens", HostLane.Fast, 0)]
    private static bool TestCreatesBindsAndListens()
    {
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketFacade facade = new(table);
        OSHostSocketCreateResult create = facade.Create(TcpCreateOptions());
        if (!create.Succeeded || create.Socket.Value != 1)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketBindRequest bind = new(
            create.Socket.AsHandle(),
            Endpoint(127, 0, 0, 1, 8080).AsSocketAddress(),
            flags: 0,
            reserved: 0);
        OSHostSocketListenRequest listen = new(
            create.Socket.AsHandle(),
            backlog: 4,
            flags: 0,
            reserved: 0);

        return HostCheck.Result(facade.Bind(bind) == OSHostStatus.Ok
            && facade.Listen(listen) == OSHostStatus.Ok
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Listening
            && entry.ListenBacklog == 4);
    }

    [HostTest("pal.socket facade accepts with endpoint output", HostLane.Fast, 1)]
    private static bool TestAcceptsWithEndpointOutput()
    {
        OSHostSocketTable table = new(capacity: 3);
        OSHostSocketFacade facade = new(table);
        OSHostSocketHandle listener = CreateListeningSocket(table, Endpoint(0, 0, 0, 0, 9090));
        OSHostIpv4Endpoint local = Endpoint(10, 0, 2, 8, 9090);
        OSHostIpv4Endpoint remote = Endpoint(203, 0, 113, 44, 51000);

        if (table.EnqueueAcceptedConnection(listener, local, remote) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketAcceptRequest acceptRequest = new(
            listener.AsHandle(),
            OSHostSocketTimeouts.Immediate,
            flags: 0,
            reserved: 0);
        OSHostStatus acceptStatus = facade.Accept(acceptRequest, out OSHostSocketAcceptOutput output);

        return HostCheck.Result(acceptStatus == OSHostStatus.Ok
            && output.AcceptedSocket.IsValid
            && output.LocalAddress.Family == OSHostSocketConstants.AddressFamilyIpv4
            && output.LocalAddress.PortNetworkOrder == local.PortNetworkOrder
            && output.LocalAddress.AddressLow == local.ToAddressNetworkOrder()
            && output.RemoteAddress.PortNetworkOrder == remote.PortNetworkOrder
            && output.RemoteAddress.AddressLow == remote.ToAddressNetworkOrder());
    }

    [HostTest("pal.socket facade connects with remote endpoint", HostLane.Fast, 2)]
    private static bool TestConnectsWithRemoteEndpoint()
    {
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketFacade facade = new(table);
        OSHostSocketCreateResult create = facade.Create(TcpCreateOptions());
        OSHostIpv4Endpoint local = Endpoint(10, 0, 2, 15, 42000);
        OSHostIpv4Endpoint remote = Endpoint(198, 51, 100, 22, 443);
        if (!create.Succeeded)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketBindRequest bind = new(
            create.Socket.AsHandle(),
            local.AsSocketAddress(),
            flags: 0,
            reserved: 0);
        OSHostSocketConnectRequest connect = new(
            create.Socket.AsHandle(),
            remote.AsSocketAddress(),
            OSHostSocketTimeouts.Immediate,
            flags: 0,
            reserved: 0);
        OSHostStatus connectStatus = facade.Bind(bind) == OSHostStatus.Ok
            ? facade.Connect(connect)
            : OSHostStatus.InternalError;
        OSHostStatus remoteStatus = facade.GetAddress(
            new OSHostSocketAddressQueryRequest(
                create.Socket.AsHandle(),
                OSHostSocketConstants.AddressKindRemote,
                flags: 0,
                reserved: 0),
            out OSHostSocketAddress remoteAddress);
        OSHostSocketConnectRequest invalidTimeout = new(
            create.Socket.AsHandle(),
            remote.AsSocketAddress(),
            new OSHostTimeout(kind: 99, flags: 0, ticks: 0, tickFrequency: 0),
            flags: 0,
            reserved: 0);

        return HostCheck.Result(connectStatus == OSHostStatus.Ok
            && remoteStatus == OSHostStatus.Ok
            && remoteAddress.AddressLow == remote.ToAddressNetworkOrder()
            && remoteAddress.PortNetworkOrder == remote.PortNetworkOrder
            && table.TryGetEntry(create.Socket, out OSHostSocketTableEntry entry) == OSHostStatus.Ok
            && entry.State == OSHostSocketLifecycleState.Connected
            && facade.Connect(invalidTimeout) == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.socket facade preserves partial io", HostLane.Fast, 3)]
    private static bool TestPreservesPartialIo()
    {
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketFacade facade = new(table);
        OSHostSocketHandle socket = CreateAcceptedSocket(table);

        byte[] receiveFirst = new byte[2];
        byte[] receiveSecond = new byte[2];
        if (table.QueueInboundData(socket, [0x41, 0x42, 0x43]) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketIoRequest receiveRequest = new(
            socket.AsHandle(),
            new OSHostByteSpan(address: 0x1000, length: 2),
            flags: 0,
            reserved: 0);
        OSHostSocketOperationResult firstReceive = facade.Receive(receiveRequest, receiveFirst);
        OSHostSocketOperationResult secondReceive = facade.Receive(receiveRequest, receiveSecond);

        byte[] sendPayload = new byte[OSHostSocketTable.ConnectedBufferCapacity + 1];
        OSHostSocketIoRequest sendRequest = new(
            socket.AsHandle(),
            new OSHostByteSpan(address: 0x2000, length: (ulong)sendPayload.Length),
            flags: 0,
            reserved: 0);
        OSHostSocketOperationResult send = facade.Send(sendRequest, sendPayload);
        OSHostSocketOperationResult sendAgain = facade.Send(sendRequest, sendPayload);

        return HostCheck.Result(firstReceive.Succeeded
            && firstReceive.BytesTransferred == 2
            && receiveFirst[0] == 0x41
            && receiveFirst[1] == 0x42
            && secondReceive.Succeeded
            && secondReceive.BytesTransferred == 1
            && receiveSecond[0] == 0x43
            && receiveSecond[1] == 0
            && send.Succeeded
            && send.BytesTransferred == OSHostSocketTable.ConnectedBufferCapacity
            && sendAgain.StatusCode == (int)OSHostStatus.WouldBlock
            && sendAgain.BytesTransferred == 0);
    }

    [HostTest("pal.socket facade polls close and reset states", HostLane.Fast, 4)]
    private static bool TestPollsCloseAndResetStates()
    {
        OSHostSocketTable closeTable = new(capacity: 2);
        OSHostSocketFacade closeFacade = new(closeTable);
        OSHostSocketHandle closeSocket = CreateAcceptedSocket(closeTable);
        if (closeTable.MarkPeerClosed(closeSocket) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketPollEntry closePoll = closeFacade.Poll(new OSHostSocketPollEntry(
            closeSocket.AsHandle(),
            OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup,
            observedEvents: 0,
            statusCode: 0,
            flags: 0));

        OSHostSocketTable resetTable = new(capacity: 2);
        OSHostSocketFacade resetFacade = new(resetTable);
        OSHostSocketHandle resetSocket = CreateAcceptedSocket(resetTable);
        if (resetTable.MarkPeerReset(resetSocket) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketPollEntry resetPoll = resetFacade.Poll(new OSHostSocketPollEntry(
            resetSocket.AsHandle(),
            OSHostSocketConstants.PollReadable
                | OSHostSocketConstants.PollWritable
                | OSHostSocketConstants.PollError
                | OSHostSocketConstants.PollHangup,
            observedEvents: 0,
            statusCode: 0,
            flags: 0));

        return HostCheck.Result(closePoll.StatusCode == (int)OSHostStatus.Ok
            && closePoll.ObservedEvents == (OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup)
            && resetPoll.StatusCode == (int)OSHostStatus.ConnectionReset
            && resetPoll.ObservedEvents == (OSHostSocketConstants.PollError | OSHostSocketConstants.PollHangup));
    }

    [HostTest("pal.socket facade translates invalid handles", HostLane.Fast, 5)]
    private static bool TestTranslatesInvalidHandles()
    {
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketFacade facade = new(table);
        OSHostHandle invalid = new(0);
        OSHostSocketIoRequest receiveRequest = new(
            invalid,
            new OSHostByteSpan(address: 0x1000, length: 1),
            flags: 0,
            reserved: 0);
        byte[] buffer = new byte[1];

        OSHostSocketOperationResult receive = facade.Receive(receiveRequest, buffer);
        OSHostSocketPollEntry poll = facade.Poll(new OSHostSocketPollEntry(
            invalid,
            OSHostSocketConstants.PollReadable,
            observedEvents: 0,
            statusCode: 0,
            flags: 0));

        return HostCheck.Result(facade.Listen(new OSHostSocketListenRequest(invalid, backlog: 1, flags: 0, reserved: 0))
                == OSHostStatus.InvalidArgument
            && receive.StatusCode == (int)OSHostStatus.InvalidArgument
            && poll.StatusCode == (int)OSHostStatus.InvalidArgument
            && poll.ObservedEvents == OSHostSocketConstants.PollError);
    }

    [HostTest("pal.socket facade close invalidates handle", HostLane.Fast, 6)]
    private static bool TestCloseInvalidatesHandle()
    {
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketFacade facade = new(table);
        OSHostSocketCreateResult create = facade.Create(TcpCreateOptions());
        if (!create.Succeeded)
        {
            return HostCheck.Result(false);
        }

        OSHostHandle handle = create.Socket.AsHandle();
        return HostCheck.Result(facade.Close(handle) == OSHostStatus.Ok
            && facade.Close(handle) == OSHostStatus.InvalidArgument
            && table.TryGetEntry(create.Socket, out _) == OSHostStatus.InvalidArgument);
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

    private static OSHostSocketHandle CreateListeningSocket(
        OSHostSocketTable table,
        OSHostIpv4Endpoint endpoint)
    {
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        if (!create.Succeeded
            || table.Bind(create.Socket, endpoint) != OSHostStatus.Ok
            || table.Listen(create.Socket, backlog: 2) != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Could not create listening socket fixture");
        }

        return create.Socket;
    }

    private static OSHostSocketHandle CreateAcceptedSocket(OSHostSocketTable table)
    {
        OSHostSocketHandle listener = CreateListeningSocket(table, Endpoint(127, 0, 0, 1, 7070));
        if (table.EnqueueAcceptedConnection(
                listener,
                Endpoint(127, 0, 0, 1, 7070),
                Endpoint(127, 0, 0, 1, 51001)) != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Could not queue accepted socket fixture");
        }

        OSHostSocketAcceptResult accept = table.Accept(listener);
        if (!accept.Succeeded)
        {
            throw new InvalidOperationException("Could not accept socket fixture");
        }

        return accept.Socket;
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
