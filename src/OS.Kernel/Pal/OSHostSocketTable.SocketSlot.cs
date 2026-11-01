namespace OS.Pal;
public sealed partial class OSHostSocketTable
{
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
        public static SocketSlot Create(OSHostSocketHandle socket, OSHostSocketCreateOptions createOptions)
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

        public static SocketSlot CreateAccepted(OSHostSocketHandle socket, OSHostSocketCreateOptions createOptions, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint, SocketOptions options)
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

        public OSHostStatus EnqueueAcceptedConnection(OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
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

            AcceptQueueHead = AcceptQueueHead == 0 ? ListenBacklog - 1 : AcceptQueueHead - 1;
            AcceptQueue[AcceptQueueHead] = pending;
            PendingAcceptCount++;
        }
    }
}
