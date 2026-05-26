namespace OS.Pal;

public enum OSHostSocketLifecycleState : uint
{
    Closed = 0,
    Created = 1,
    Bound = 2,
    Listening = 3,
    Connected = 4
}

public readonly struct OSHostSocketCreateResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostSocketHandle Socket;

    public OSHostSocketCreateResult(OSHostStatus status, OSHostSocketHandle socket)
    {
        Status = status;
        Socket = socket;
    }

    public bool Succeeded => Status == OSHostStatus.Ok && Socket.IsValid;

    public static OSHostSocketCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostSocketCreateResult(status, default);
    }
}

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

    public OSHostSocketTableEntry(
        OSHostSocketHandle socket,
        OSHostSocketLifecycleState state,
        OSHostSocketCreateOptions createOptions,
        OSHostIpv4Endpoint localEndpoint,
        OSHostIpv4Endpoint remoteEndpoint,
        OSHostSocketOptionValue noDelay,
        OSHostSocketOptionValue reuseAddress,
        OSHostSocketOptionValue receiveTimeoutMilliseconds,
        OSHostSocketOptionValue sendTimeoutMilliseconds,
        OSHostSocketOptionValue keepAlive,
        int listenBacklog,
        int pendingAcceptCount)
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

public readonly struct OSHostSocketAcceptResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostSocketHandle Socket;

    public readonly OSHostIpv4Endpoint LocalEndpoint;

    public readonly OSHostIpv4Endpoint RemoteEndpoint;

    public OSHostSocketAcceptResult(
        OSHostStatus status,
        OSHostSocketHandle socket,
        OSHostIpv4Endpoint localEndpoint,
        OSHostIpv4Endpoint remoteEndpoint)
    {
        Status = status;
        Socket = socket;
        LocalEndpoint = localEndpoint;
        RemoteEndpoint = remoteEndpoint;
    }

    public bool Succeeded =>
        Status == OSHostStatus.Ok &&
        Socket.IsValid &&
        LocalEndpoint.IsValid &&
        RemoteEndpoint.IsValid;

    public static OSHostSocketAcceptResult FromStatus(OSHostStatus status)
    {
        return new OSHostSocketAcceptResult(status, default, default, default);
    }
}

public readonly struct OSHostSocketPollResult
{
    public readonly OSHostStatus Status;

    public readonly uint ObservedEvents;

    public readonly bool IsInitialized;

    public OSHostSocketPollResult(OSHostStatus status, uint observedEvents, bool isInitialized = true)
    {
        Status = status;
        ObservedEvents = observedEvents;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;
}

public sealed class OSHostSocketTable
{
    public const uint CreateOptionsVersion = 1;

    public const int MaxListenBacklog = 128;

    public const int ConnectedBufferCapacity = 4096;

    private readonly SocketSlot[] slots;

    public OSHostSocketTable(int capacity)
    {
        slots = capacity > 0
            ? new SocketSlot[capacity]
            : [];
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

    public OSHostStatus EnqueueAcceptedConnection(
        OSHostSocketHandle listener,
        OSHostIpv4Endpoint localEndpoint,
        OSHostIpv4Endpoint remoteEndpoint)
    {
        OSHostStatus socketStatus = TryFindListeningSlot(listener, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (OSHostIpv4Endpoint.Validate(localEndpoint) != OSHostStatus.Ok
            || OSHostIpv4Endpoint.Validate(remoteEndpoint) != OSHostStatus.Ok)
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
        slots[acceptedSlotIndex] = SocketSlot.CreateAccepted(
            acceptedSocket,
            slots[listenerSlotIndex].CreateOptions,
            pending.LocalEndpoint,
            pending.RemoteEndpoint,
            slots[listenerSlotIndex].Options);

        return new OSHostSocketAcceptResult(
            OSHostStatus.Ok,
            acceptedSocket,
            pending.LocalEndpoint,
            pending.RemoteEndpoint);
    }

    public OSHostStatus Connect(
        OSHostSocketHandle socket,
        OSHostIpv4Endpoint remoteEndpoint)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        if (OSHostIpv4Endpoint.Validate(remoteEndpoint) != OSHostStatus.Ok ||
            IsAnyAddress(remoteEndpoint))
        {
            return OSHostStatus.InvalidArgument;
        }

        if (slots[slotIndex].State != OSHostSocketLifecycleState.Bound)
        {
            return slots[slotIndex].State == OSHostSocketLifecycleState.Created
                ? OSHostStatus.AddressNotAvailable
                : OSHostStatus.InvalidArgument;
        }

        slots[slotIndex].Connect(remoteEndpoint);
        return OSHostStatus.Ok;
    }

    public OSHostSocketOperationResult Send(
        OSHostSocketHandle socket,
        ReadOnlySpan<byte> source)
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

    public OSHostSocketOperationResult Receive(
        OSHostSocketHandle socket,
        Span<byte> destination)
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
        uint knownEvents = OSHostSocketConstants.PollReadable
            | OSHostSocketConstants.PollWritable
            | OSHostSocketConstants.PollError
            | OSHostSocketConstants.PollHangup
            | OSHostSocketConstants.PollAccept;
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
                return new OSHostSocketPollResult(
                    OSHostStatus.ConnectionReset,
                    observed & requestedEvents);
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

    public OSHostStatus SetSocketOption(
        OSHostSocketHandle socket,
        uint optionName,
        OSHostSocketOptionValue value)
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

    public OSHostStatus GetSocketOption(
        OSHostSocketHandle socket,
        uint optionName,
        out OSHostSocketOptionValue value)
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

    public OSHostStatus GetLocalEndpoint(
        OSHostSocketHandle socket,
        out OSHostIpv4Endpoint endpoint)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            endpoint = default;
            return socketStatus;
        }

        SocketSlot slot = slots[slotIndex];
        if (slot.State is not (OSHostSocketLifecycleState.Bound
            or OSHostSocketLifecycleState.Listening
            or OSHostSocketLifecycleState.Connected))
        {
            endpoint = default;
            return OSHostStatus.InvalidArgument;
        }

        endpoint = slot.LocalEndpoint;
        return OSHostStatus.Ok;
    }

    public OSHostStatus GetRemoteEndpoint(
        OSHostSocketHandle socket,
        out OSHostIpv4Endpoint endpoint)
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
        return written == source.Length
            ? OSHostStatus.Ok
            : OSHostStatus.WouldBlock;
    }

    public OSHostSocketOperationResult DrainOutboundData(
        OSHostSocketHandle socket,
        Span<byte> destination)
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

    public OSHostStatus TryGetEntry(
        OSHostSocketHandle socket,
        out OSHostSocketTableEntry entry)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out int slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            entry = default;
            return socketStatus;
        }

        SocketSlot slot = slots[slotIndex];
        entry = new OSHostSocketTableEntry(
            slot.Socket,
            slot.State,
            slot.CreateOptions,
            slot.LocalEndpoint,
            slot.RemoteEndpoint,
            slot.Options.NoDelay,
            slot.Options.ReuseAddress,
            slot.Options.ReceiveTimeoutMilliseconds,
            slot.Options.SendTimeoutMilliseconds,
            slot.Options.KeepAlive,
            slot.ListenBacklog,
            slot.PendingAcceptCount);
        return OSHostStatus.Ok;
    }

    private static bool IsSupportedCreateOptions(OSHostSocketCreateOptions options)
    {
        if (options.Version != CreateOptionsVersion || options.Reserved != 0)
        {
            return false;
        }

        if (options.Domain != OSHostSocketConstants.DomainIpv4
            || options.Type != OSHostSocketConstants.TypeStream
            || options.Protocol != OSHostSocketConstants.ProtocolTcp)
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
        return slots[slotIndex].InUse && slots[slotIndex].Socket.Value == socket.Value
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }

    private OSHostStatus TryFindListeningSlot(OSHostSocketHandle socket, out int slotIndex)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        return slots[slotIndex].State == OSHostSocketLifecycleState.Listening
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }

    private OSHostStatus TryFindConnectedSlot(OSHostSocketHandle socket, out int slotIndex)
    {
        OSHostStatus socketStatus = TryFindSlot(socket, out slotIndex);
        if (socketStatus != OSHostStatus.Ok)
        {
            return socketStatus;
        }

        return slots[slotIndex].State == OSHostSocketLifecycleState.Connected
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
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

            if ((slots[i].State is OSHostSocketLifecycleState.Bound
                or OSHostSocketLifecycleState.Listening)
                && EndpointsConflict(slots[i].LocalEndpoint, endpoint))
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

    private static bool EndpointsConflict(
        OSHostIpv4Endpoint first,
        OSHostIpv4Endpoint second)
    {
        if (first.PortNetworkOrder != second.PortNetworkOrder)
        {
            return false;
        }

        return IsAnyAddress(first)
            || IsAnyAddress(second)
            || (first.Address0 == second.Address0
                && first.Address1 == second.Address1
                && first.Address2 == second.Address2
                && first.Address3 == second.Address3);
    }

    private static bool IsAnyAddress(OSHostIpv4Endpoint endpoint)
    {
        return endpoint.Address0 == 0
            && endpoint.Address1 == 0
            && endpoint.Address2 == 0
            && endpoint.Address3 == 0;
    }

    private static bool AcceptedLocalMatchesListener(
        OSHostIpv4Endpoint listener,
        OSHostIpv4Endpoint localEndpoint)
    {
        if (listener.PortNetworkOrder != localEndpoint.PortNetworkOrder)
        {
            return false;
        }

        return IsAnyAddress(listener)
            || (listener.Address0 == localEndpoint.Address0
                && listener.Address1 == localEndpoint.Address1
                && listener.Address2 == localEndpoint.Address2
                && listener.Address3 == localEndpoint.Address3);
    }

    private static OSHostSocketHandle CreateHandleForSlot(int slotIndex)
    {
        return new OSHostSocketHandle((ulong)slotIndex + 1);
    }

    private readonly struct PendingAcceptedConnection
    {
        public readonly OSHostIpv4Endpoint LocalEndpoint;

        public readonly OSHostIpv4Endpoint RemoteEndpoint;

        public PendingAcceptedConnection(
            OSHostIpv4Endpoint localEndpoint,
            OSHostIpv4Endpoint remoteEndpoint)
        {
            LocalEndpoint = localEndpoint;
            RemoteEndpoint = remoteEndpoint;
        }
    }

    private struct SocketSlot
    {
        public bool InUse;

        public OSHostSocketHandle Socket;

        public OSHostSocketLifecycleState State;

        public OSHostSocketCreateOptions CreateOptions;

        public OSHostIpv4Endpoint LocalEndpoint;

        public OSHostIpv4Endpoint RemoteEndpoint;

        public SocketOptions Options;

        public int ListenBacklog;

        public PendingAcceptedConnection[]? AcceptQueue;

        public SocketBuffers? Buffers;

        public int AcceptQueueHead;

        public int PendingAcceptCount;

        public bool ReceiveShutdown;

        public bool SendShutdown;

        public bool PeerClosed;

        public bool PeerReset;

        public static SocketSlot Create(
            OSHostSocketHandle socket,
            OSHostSocketCreateOptions createOptions)
        {
            return new SocketSlot
            {
                InUse = true,
                Socket = socket,
                State = OSHostSocketLifecycleState.Created,
                CreateOptions = createOptions,
                LocalEndpoint = default,
                RemoteEndpoint = default,
                Options = SocketOptions.Default,
                ListenBacklog = 0,
                AcceptQueue = null,
                Buffers = null,
                AcceptQueueHead = 0,
                PendingAcceptCount = 0,
                ReceiveShutdown = false,
                SendShutdown = false,
                PeerClosed = false,
                PeerReset = false
            };
        }

        public static SocketSlot CreateAccepted(
            OSHostSocketHandle socket,
            OSHostSocketCreateOptions createOptions,
            OSHostIpv4Endpoint localEndpoint,
            OSHostIpv4Endpoint remoteEndpoint,
            SocketOptions options)
        {
            return new SocketSlot
            {
                InUse = true,
                Socket = socket,
                State = OSHostSocketLifecycleState.Connected,
                CreateOptions = createOptions,
                LocalEndpoint = localEndpoint,
                RemoteEndpoint = remoteEndpoint,
                Options = options,
                ListenBacklog = 0,
                AcceptQueue = null,
                Buffers = new SocketBuffers(ConnectedBufferCapacity),
                AcceptQueueHead = 0,
                PendingAcceptCount = 0,
                ReceiveShutdown = false,
                SendShutdown = false,
                PeerClosed = false,
                PeerReset = false
            };
        }

        public void Bind(OSHostIpv4Endpoint endpoint)
        {
            LocalEndpoint = endpoint;
            State = OSHostSocketLifecycleState.Bound;
        }

        public void Listen(int backlog)
        {
            ListenBacklog = backlog;
            AcceptQueue = new PendingAcceptedConnection[backlog];
            AcceptQueueHead = 0;
            PendingAcceptCount = 0;
            State = OSHostSocketLifecycleState.Listening;
        }

        public void Connect(OSHostIpv4Endpoint remoteEndpoint)
        {
            RemoteEndpoint = remoteEndpoint;
            ListenBacklog = 0;
            AcceptQueue = null;
            AcceptQueueHead = 0;
            PendingAcceptCount = 0;
            Buffers = new SocketBuffers(ConnectedBufferCapacity);
            State = OSHostSocketLifecycleState.Connected;
        }

        public void Shutdown(uint flags)
        {
            if ((flags & OSHostSocketConstants.ShutdownReceive) != 0)
            {
                ReceiveShutdown = true;
            }

            if ((flags & OSHostSocketConstants.ShutdownSend) != 0)
            {
                SendShutdown = true;
            }
        }

        public void MarkPeerClosed()
        {
            PeerClosed = true;
        }

        public void MarkPeerReset()
        {
            PeerReset = true;
            PeerClosed = false;
        }

        public void SetOption(uint optionName, OSHostSocketOptionValue value)
        {
            Options = Options.Set(optionName, value);
        }

        public OSHostStatus TryGetOption(uint optionName, out OSHostSocketOptionValue value)
        {
            return Options.TryGet(optionName, out value);
        }

        public OSHostStatus EnqueueAcceptedConnection(
            OSHostIpv4Endpoint localEndpoint,
            OSHostIpv4Endpoint remoteEndpoint)
        {
            if (AcceptQueue is null || PendingAcceptCount >= ListenBacklog)
            {
                return OSHostStatus.WouldBlock;
            }

            int tail = (AcceptQueueHead + PendingAcceptCount) % ListenBacklog;
            AcceptQueue[tail] = new PendingAcceptedConnection(localEndpoint, remoteEndpoint);
            PendingAcceptCount++;
            return OSHostStatus.Ok;
        }

        public bool TryDequeueAcceptedConnection(out PendingAcceptedConnection pending)
        {
            if (AcceptQueue is null || PendingAcceptCount == 0)
            {
                pending = default;
                return false;
            }

            pending = AcceptQueue[AcceptQueueHead];
            AcceptQueue[AcceptQueueHead] = default;
            AcceptQueueHead = (AcceptQueueHead + 1) % ListenBacklog;
            PendingAcceptCount--;
            if (PendingAcceptCount == 0)
            {
                AcceptQueueHead = 0;
            }

            return true;
        }

        public void PushAcceptedConnectionFront(PendingAcceptedConnection pending)
        {
            if (AcceptQueue is null || PendingAcceptCount >= ListenBacklog)
            {
                return;
            }

            AcceptQueueHead = AcceptQueueHead == 0
                ? ListenBacklog - 1
                : AcceptQueueHead - 1;
            AcceptQueue[AcceptQueueHead] = pending;
            PendingAcceptCount++;
        }
    }

    private readonly struct SocketOptions
    {
        public readonly OSHostSocketOptionValue NoDelay;

        public readonly OSHostSocketOptionValue ReuseAddress;

        public readonly OSHostSocketOptionValue ReceiveTimeoutMilliseconds;

        public readonly OSHostSocketOptionValue SendTimeoutMilliseconds;

        public readonly OSHostSocketOptionValue KeepAlive;

        public SocketOptions(
            OSHostSocketOptionValue noDelay,
            OSHostSocketOptionValue reuseAddress,
            OSHostSocketOptionValue receiveTimeoutMilliseconds,
            OSHostSocketOptionValue sendTimeoutMilliseconds,
            OSHostSocketOptionValue keepAlive)
        {
            NoDelay = noDelay;
            ReuseAddress = reuseAddress;
            ReceiveTimeoutMilliseconds = receiveTimeoutMilliseconds;
            SendTimeoutMilliseconds = sendTimeoutMilliseconds;
            KeepAlive = keepAlive;
        }

        public static SocketOptions Default => new(
            OSHostSocketOptionValue.Boolean(enabled: false),
            OSHostSocketOptionValue.Boolean(enabled: false),
            OSHostSocketOptionValue.TimeoutMilliseconds(-1),
            OSHostSocketOptionValue.TimeoutMilliseconds(-1),
            OSHostSocketOptionValue.Boolean(enabled: false));

        public SocketOptions Set(uint optionName, OSHostSocketOptionValue value)
        {
            return optionName switch
            {
                OSHostSocketConstants.SocketOptionNoDelay => new SocketOptions(
                    value,
                    ReuseAddress,
                    ReceiveTimeoutMilliseconds,
                    SendTimeoutMilliseconds,
                    KeepAlive),
                OSHostSocketConstants.SocketOptionReuseAddress => new SocketOptions(
                    NoDelay,
                    value,
                    ReceiveTimeoutMilliseconds,
                    SendTimeoutMilliseconds,
                    KeepAlive),
                OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds => new SocketOptions(
                    NoDelay,
                    ReuseAddress,
                    value,
                    SendTimeoutMilliseconds,
                    KeepAlive),
                OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds => new SocketOptions(
                    NoDelay,
                    ReuseAddress,
                    ReceiveTimeoutMilliseconds,
                    value,
                    KeepAlive),
                OSHostSocketConstants.SocketOptionKeepAlive => new SocketOptions(
                    NoDelay,
                    ReuseAddress,
                    ReceiveTimeoutMilliseconds,
                    SendTimeoutMilliseconds,
                    value),
                _ => this
            };
        }

        public OSHostStatus TryGet(uint optionName, out OSHostSocketOptionValue value)
        {
            switch (optionName)
            {
                case OSHostSocketConstants.SocketOptionNoDelay:
                    value = NoDelay;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionReuseAddress:
                    value = ReuseAddress;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds:
                    value = ReceiveTimeoutMilliseconds;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds:
                    value = SendTimeoutMilliseconds;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionKeepAlive:
                    value = KeepAlive;
                    return OSHostStatus.Ok;
                default:
                    value = default;
                    return OSHostStatus.InvalidArgument;
            }
        }
    }

    private sealed class SocketBuffers
    {
        private readonly byte[] inbound;

        private readonly byte[] outbound;

        private int inboundHead;

        private int inboundLength;

        private int outboundHead;

        private int outboundLength;

        public SocketBuffers(int capacity)
        {
            inbound = new byte[capacity];
            outbound = new byte[capacity];
        }

        public int InboundLength => inboundLength;

        public int InboundAvailable => inbound.Length - inboundLength;

        public int OutboundLength => outboundLength;

        public int OutboundAvailable => outbound.Length - outboundLength;

        public int WriteInbound(ReadOnlySpan<byte> source)
        {
            return Write(source, inbound, ref inboundHead, ref inboundLength);
        }

        public int ReadInbound(Span<byte> destination)
        {
            return Read(destination, inbound, ref inboundHead, ref inboundLength);
        }

        public int WriteOutbound(ReadOnlySpan<byte> source)
        {
            return Write(source, outbound, ref outboundHead, ref outboundLength);
        }

        public int ReadOutbound(Span<byte> destination)
        {
            return Read(destination, outbound, ref outboundHead, ref outboundLength);
        }

        private static int Write(
            ReadOnlySpan<byte> source,
            byte[] buffer,
            ref int head,
            ref int length)
        {
            int count = Math.Min(source.Length, buffer.Length - length);
            for (int i = 0; i < count; i++)
            {
                int tail = (head + length) % buffer.Length;
                buffer[tail] = source[i];
                length++;
            }

            return count;
        }

        private static int Read(
            Span<byte> destination,
            byte[] buffer,
            ref int head,
            ref int length)
        {
            int count = Math.Min(destination.Length, length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = buffer[head];
                buffer[head] = 0;
                head = (head + 1) % buffer.Length;
                length--;
            }

            if (length == 0)
            {
                head = 0;
            }

            return count;
        }
    }
}
