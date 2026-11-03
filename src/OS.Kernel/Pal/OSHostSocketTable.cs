namespace OS.Pal;

public sealed partial class OSHostSocketTable
{
    public const uint CreateOptionsVersion = 1;
    public const int MaxListenBacklog = 128;
    public const int ConnectedBufferCapacity = 4096;
    private readonly SocketSlot[] slots;
    public OSHostSocketTable(int capacity)
    {
        slots = capacity > 0 ? new SocketSlot[capacity] : [];
    }

    public int Capacity => slots.Length;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].InUse)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public OSHostSocketCreateResult Create(OSHostSocketCreateOptions options)
    {
        if (!IsSupportedCreateOptions(options))
        {
            return OSHostSocketCreateResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].InUse)
            {
                continue;
            }

            OSHostSocketHandle socket = CreateHandleForSlot(i);
            slots[i] = SocketSlot.Create(socket, options);
            return new OSHostSocketCreateResult(OSHostStatus.Ok, socket);
        }

        return OSHostSocketCreateResult.FromStatus(OSHostStatus.OutOfMemory);
    }

    public OSHostStatus Bind(OSHostSocketHandle socket, OSHostIpv4Endpoint endpoint)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (OSHostIpv4Endpoint.Validate(endpoint) != OSHostStatus.Ok)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (slots[slotIndex].State != OSHostSocketLifecycleState.Created)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (IsEndpointInUse(endpoint, slots[slotIndex].Options.ReuseAddress.IsBooleanEnabled))
        {
            return OSHostStatus.AddressInUse;
        }

        slots[slotIndex].Bind(endpoint);
        return OSHostStatus.Ok;
    }

    public OSHostStatus Listen(OSHostSocketHandle socket, int backlog)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (backlog <= 0 || backlog > MaxListenBacklog)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (slots[slotIndex].State != OSHostSocketLifecycleState.Bound)
        {
            return OSHostStatus.InvalidArgument;
        }

        slots[slotIndex].Listen(backlog);
        return OSHostStatus.Ok;
    }

    public OSHostStatus EnqueueAcceptedConnection(OSHostSocketHandle listener, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
    {
        OSHostStatus socketStatus = TryFindListeningSlot(listener, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (OSHostIpv4Endpoint.Validate(localEndpoint) != OSHostStatus.Ok || OSHostIpv4Endpoint.Validate(remoteEndpoint) != OSHostStatus.Ok)
        {
            return OSHostStatus.InvalidArgument;
        }

        SocketSlot slot = slots[slotIndex];
        if (!AcceptedLocalMatchesListener(slot.LocalEndpoint, localEndpoint))
        {
            return OSHostStatus.InvalidArgument;
        }

        return slots[slotIndex].EnqueueAcceptedConnection(localEndpoint, remoteEndpoint);
    }

    public OSHostSocketAcceptResult Accept(OSHostSocketHandle listener)
    {
        OSHostStatus socketStatus = TryFindListeningSlot(listener, out int listenerSlotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return OSHostSocketAcceptResult.FromStatus(socketStatus);
        }

        if (!slots[listenerSlotIndex].TryDequeueAcceptedConnection(out PendingAcceptedConnection pending))
        {
            return OSHostSocketAcceptResult.FromStatus(OSHostStatus.WouldBlock);
        }

        int acceptedSlotIndex = FindFreeSlot();
        if (acceptedSlotIndex < 0)
        {
            slots[listenerSlotIndex].PushAcceptedConnectionFront(pending);
            return OSHostSocketAcceptResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        OSHostSocketHandle acceptedSocket = CreateHandleForSlot(acceptedSlotIndex);
        slots[acceptedSlotIndex] = SocketSlot.CreateAccepted(acceptedSocket, slots[listenerSlotIndex].CreateOptions, pending.LocalEndpoint, pending.RemoteEndpoint, slots[listenerSlotIndex].Options);
        return new OSHostSocketAcceptResult(OSHostStatus.Ok, acceptedSocket, pending.LocalEndpoint, pending.RemoteEndpoint);
    }

    public OSHostStatus Connect(OSHostSocketHandle socket, OSHostIpv4Endpoint remoteEndpoint)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (OSHostIpv4Endpoint.Validate(remoteEndpoint) != OSHostStatus.Ok || IsAnyAddress(remoteEndpoint))
        {
            return OSHostStatus.InvalidArgument;
        }

        if (slots[slotIndex].State != OSHostSocketLifecycleState.Bound)
        {
            return slots[slotIndex].State == OSHostSocketLifecycleState.Created ? OSHostStatus.AddressNotAvailable : OSHostStatus.InvalidArgument;
        }

        slots[slotIndex].Connect(remoteEndpoint);
        return OSHostStatus.Ok;
    }

    public OSHostSocketOperationResult Send(OSHostSocketHandle socket, ReadOnlySpan<byte> source)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return OSHostSocketOperationResult.FromStatus(socketStatus, 0, socket);
        }

        SocketSlot slot = slots[slotIndex];
        if (slot.PeerReset)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.ConnectionReset, 0, socket);
        }

        if (slot.SendShutdown)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.Interrupted, 0, socket);
        }

        if (source.Length == 0)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.Ok, 0, socket);
        }

        int written = slot.Buffers?.WriteOutbound(source) ?? 0;
        if (written == 0)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.WouldBlock, 0, socket);
        }

        return OSHostSocketOperationResult.FromStatus(OSHostStatus.Ok, (ulong)written, socket);
    }

    public OSHostSocketOperationResult Receive(OSHostSocketHandle socket, Span<byte> destination)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return OSHostSocketOperationResult.FromStatus(socketStatus, 0, socket);
        }

        SocketSlot slot = slots[slotIndex];
        if (slot.PeerReset)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.ConnectionReset, 0, socket);
        }

        if (slot.ReceiveShutdown)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.Interrupted, 0, socket);
        }

        if (slot.Buffers is null || slot.Buffers.InboundLength == 0)
        {
            if (slot.PeerClosed)
            {
                return OSHostSocketOperationResult.FromStatus(OSHostStatus.EndOfFile, 0, socket);
            }

            return OSHostSocketOperationResult.FromStatus(OSHostStatus.WouldBlock, 0, socket);
        }

        if (destination.Length == 0)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.BufferTooSmall, 0, socket);
        }

        int read = slot.Buffers.ReadInbound(destination);
        return OSHostSocketOperationResult.FromStatus(OSHostStatus.Ok, (ulong)read, socket);
    }

    public OSHostSocketPollResult Poll(OSHostSocketHandle socket, uint requestedEvents)
    {
        uint knownEvents = OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollWritable | OSHostSocketConstants.PollError | OSHostSocketConstants.PollHangup | OSHostSocketConstants.PollAccept;
        if ((requestedEvents & ~knownEvents) != 0)
        {
            return new OSHostSocketPollResult(OSHostStatus.InvalidArgument, OSHostSocketConstants.PollError);
        }

        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return new OSHostSocketPollResult(socketStatus, OSHostSocketConstants.PollError);
        }

        SocketSlot slot = slots[slotIndex];
        uint observed = 0;
        if (slot.State == OSHostSocketLifecycleState.Listening && slot.PendingAcceptCount > 0)
        {
            observed |= OSHostSocketConstants.PollAccept | OSHostSocketConstants.PollReadable;
        }

        if (slot.State == OSHostSocketLifecycleState.Connected)
        {
            if (slot.PeerReset)
            {
                observed |= OSHostSocketConstants.PollError | OSHostSocketConstants.PollHangup;
                return new OSHostSocketPollResult(OSHostStatus.ConnectionReset, observed & requestedEvents);
            }

            if (slot.Buffers is not null && slot.Buffers.InboundLength > 0 && !slot.ReceiveShutdown)
            {
                observed |= OSHostSocketConstants.PollReadable;
            }

            if (slot.PeerClosed && !slot.ReceiveShutdown)
            {
                observed |= OSHostSocketConstants.PollReadable | OSHostSocketConstants.PollHangup;
            }

            if (slot.Buffers is not null && slot.Buffers.OutboundAvailable > 0 && !slot.SendShutdown)
            {
                observed |= OSHostSocketConstants.PollWritable;
            }

            if (slot.ReceiveShutdown || slot.SendShutdown)
            {
                observed |= OSHostSocketConstants.PollHangup;
            }
        }

        return new OSHostSocketPollResult(OSHostStatus.Ok, observed & requestedEvents);
    }

    public OSHostStatus MarkPeerClosed(OSHostSocketHandle socket)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (slots[slotIndex].PeerReset)
        {
            return OSHostStatus.ConnectionReset;
        }

        slots[slotIndex].MarkPeerClosed();
        return OSHostStatus.Ok;
    }

    public OSHostStatus MarkPeerReset(OSHostSocketHandle socket)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        slots[slotIndex].MarkPeerReset();
        return OSHostStatus.Ok;
    }

    public OSHostStatus SetSocketOption(OSHostSocketHandle socket, uint optionName, OSHostSocketOptionValue value)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        OSHostStatus optionStatus = OSHostSocketOptions.Validate(optionName, value);
        if (optionStatus != OSHostStatus.Ok)
        {
            return optionStatus;
        }

        slots[slotIndex].SetOption(optionName, value);
        return OSHostStatus.Ok;
    }

    public OSHostStatus GetSocketOption(OSHostSocketHandle socket, uint optionName, out OSHostSocketOptionValue value)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            value = default;
            return socketStatus;
        }

        if (!OSHostSocketOptions.IsDefined(optionName))
        {
            value = default;
            return OSHostStatus.InvalidArgument;
        }

        return slots[slotIndex].TryGetOption(optionName, out value);
    }

    public OSHostStatus GetLocalEndpoint(OSHostSocketHandle socket, out OSHostIpv4Endpoint endpoint)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            endpoint = default;
            return socketStatus;
        }

        SocketSlot slot = slots[slotIndex];
        if (slot.State is not (OSHostSocketLifecycleState.Bound or OSHostSocketLifecycleState.Listening or OSHostSocketLifecycleState.Connected))
        {
            endpoint = default;
            return OSHostStatus.InvalidArgument;
        }

        endpoint = slot.LocalEndpoint;
        return OSHostStatus.Ok;
    }

    public OSHostStatus GetRemoteEndpoint(OSHostSocketHandle socket, out OSHostIpv4Endpoint endpoint)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            endpoint = default;
            return socketStatus;
        }

        endpoint = slots[slotIndex].RemoteEndpoint;
        return OSHostStatus.Ok;
    }

    public OSHostStatus Shutdown(OSHostSocketHandle socket, uint flags)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if ((flags & ~OSHostSocketConstants.ShutdownBoth) != 0 || flags == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        slots[slotIndex].Shutdown(flags);
        return OSHostStatus.Ok;
    }

    public OSHostStatus QueueInboundData(OSHostSocketHandle socket, ReadOnlySpan<byte> source)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (slots[slotIndex].ReceiveShutdown)
        {
            return OSHostStatus.Interrupted;
        }

        if (slots[slotIndex].PeerReset)
        {
            return OSHostStatus.ConnectionReset;
        }

        if (slots[slotIndex].PeerClosed)
        {
            return OSHostStatus.EndOfFile;
        }

        if (source.Length == 0)
        {
            return OSHostStatus.Ok;
        }

        SocketBuffers? buffers = slots[slotIndex].Buffers;
        if (buffers is null || buffers.InboundAvailable < source.Length)
        {
            return OSHostStatus.WouldBlock;
        }

        int written = buffers.WriteInbound(source);
        return written == source.Length ? OSHostStatus.Ok : OSHostStatus.WouldBlock;
    }

    public OSHostSocketOperationResult DrainOutboundData(OSHostSocketHandle socket, Span<byte> destination)
    {
        OSHostStatus socketStatus = TryFindConnectedSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return OSHostSocketOperationResult.FromStatus(socketStatus, 0, socket);
        }

        SocketBuffers? buffers = slots[slotIndex].Buffers;
        if (slots[slotIndex].PeerReset)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.ConnectionReset, 0, socket);
        }

        if (buffers is null || buffers.OutboundLength == 0)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.WouldBlock, 0, socket);
        }

        if (destination.Length == 0)
        {
            return OSHostSocketOperationResult.FromStatus(OSHostStatus.BufferTooSmall, 0, socket);
        }

        int read = buffers.ReadOutbound(destination);
        return OSHostSocketOperationResult.FromStatus(OSHostStatus.Ok, (ulong)read, socket);
    }

    public OSHostStatus Close(OSHostSocketHandle socket)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        slots[slotIndex] = default;
        return OSHostStatus.Ok;
    }

    public OSHostStatus TryGetEntry(OSHostSocketHandle socket, out OSHostSocketTableEntry entry)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            entry = default;
            return socketStatus;
        }

        SocketSlot slot = slots[slotIndex];
        entry = new OSHostSocketTableEntry(slot.Socket, slot.State, slot.CreateOptions, slot.LocalEndpoint, slot.RemoteEndpoint, slot.Options.NoDelay, slot.Options.ReuseAddress, slot.Options.ReceiveTimeoutMilliseconds, slot.Options.SendTimeoutMilliseconds, slot.Options.KeepAlive, slot.ListenBacklog, slot.PendingAcceptCount);
        return OSHostStatus.Ok;
    }

    private static bool IsSupportedCreateOptions(OSHostSocketCreateOptions options)
    {
        if (options.Version != CreateOptionsVersion || options.Reserved != 0)
        {
            return false;
        }

        if (options.Domain != OSHostSocketConstants.DomainIpv4 || options.Type != OSHostSocketConstants.TypeStream || options.Protocol != OSHostSocketConstants.ProtocolTcp)
        {
            return false;
        }

        return (options.Flags & ~OSHostSocketConstants.CreateFlagNonBlocking) == 0;
    }

    private OSHostStatus TryFindSlot(OSHostSocketHandle socket, out int slotIndex)
    {
        if (!socket.IsValid)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        ulong indexValue = socket.Value - 1;
        if (indexValue >= (ulong)slots.Length)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        slotIndex = (int)indexValue;
        return slots[slotIndex].InUse && slots[slotIndex].Socket.Value == socket.Value ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }

    private OSHostStatus TryFindListeningSlot(OSHostSocketHandle socket, out int slotIndex)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        return slots[slotIndex].State == OSHostSocketLifecycleState.Listening ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }

    private OSHostStatus TryFindConnectedSlot(OSHostSocketHandle socket, out int slotIndex)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        return slots[slotIndex].State == OSHostSocketLifecycleState.Connected ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].InUse)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsEndpointInUse(OSHostIpv4Endpoint endpoint, bool reuseAddressRequested)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].InUse)
            {
                continue;
            }

            if ((slots[i].State is OSHostSocketLifecycleState.Bound or OSHostSocketLifecycleState.Listening) && EndpointsConflict(slots[i].LocalEndpoint, endpoint))
            {
                if (reuseAddressRequested && slots[i].Options.ReuseAddress.IsBooleanEnabled)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool EndpointsConflict(OSHostIpv4Endpoint first, OSHostIpv4Endpoint second)
    {
        if (first.PortNetworkOrder != second.PortNetworkOrder)
        {
            return false;
        }

        return IsAnyAddress(first) || IsAnyAddress(second) || (first.Address0 == second.Address0 && first.Address1 == second.Address1 && first.Address2 == second.Address2 && first.Address3 == second.Address3);
    }

    private static bool IsAnyAddress(OSHostIpv4Endpoint endpoint)
    {
        return endpoint.Address0 == 0 && endpoint.Address1 == 0 && endpoint.Address2 == 0 && endpoint.Address3 == 0;
    }

    private static bool AcceptedLocalMatchesListener(OSHostIpv4Endpoint listener, OSHostIpv4Endpoint localEndpoint)
    {
        if (listener.PortNetworkOrder != localEndpoint.PortNetworkOrder)
        {
            return false;
        }

        return IsAnyAddress(listener) || (listener.Address0 == localEndpoint.Address0 && listener.Address1 == localEndpoint.Address1 && listener.Address2 == localEndpoint.Address2 && listener.Address3 == localEndpoint.Address3);
    }

    private static OSHostSocketHandle CreateHandleForSlot(int slotIndex)
    {
        return new OSHostSocketHandle((ulong)slotIndex + 1);
    }
}
