using OS.Pal;

namespace OS.Net;

public enum TcpEstablishedStreamSocketBridgeStatus
{
    Success = 0,
    MissingSocketTable = 1,
    ReceiveNotSuccessful = 2,
    PayloadLengthMismatch = 3,
    SocketQueueFailed = 4
}

public readonly struct TcpEstablishedStreamSocketBridgeResult
{
    private TcpEstablishedStreamSocketBridgeResult(
        TcpEstablishedStreamSocketBridgeStatus status,
        TcpEstablishedStreamStatus receiveStatus,
        OSHostStatus socketStatus,
        OSHostSocketHandle socket,
        int payloadBytesAccepted,
        int payloadBytesQueued,
        int unreadBytes,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        ReceiveStatus = receiveStatus;
        SocketStatus = socketStatus;
        Socket = socket;
        PayloadBytesAccepted = payloadBytesAccepted;
        PayloadBytesQueued = payloadBytesQueued;
        UnreadBytes = unreadBytes;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpEstablishedStreamSocketBridgeStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpEstablishedStreamSocketBridgeStatus.Success;

    public TcpEstablishedStreamStatus ReceiveStatus { get; }

    public OSHostStatus SocketStatus { get; }

    public OSHostSocketHandle Socket { get; }

    public int PayloadBytesAccepted { get; }

    public int PayloadBytesQueued { get; }

    public int UnreadBytes { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public static TcpEstablishedStreamSocketBridgeResult Create(
        TcpEstablishedStreamSocketBridgeStatus status,
        TcpEstablishedStreamStatus receiveStatus,
        OSHostStatus socketStatus,
        OSHostSocketHandle socket,
        int payloadBytesAccepted,
        int payloadBytesQueued,
        int unreadBytes,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamSocketBridgeResult(
            status,
            receiveStatus,
            socketStatus,
            socket,
            payloadBytesAccepted,
            payloadBytesQueued,
            unreadBytes,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }
}

public static class TcpEstablishedStreamSocketBridge
{
    public const bool UsesTcpReceiveResult = true;
    public const bool UsesCallerOwnedPayload = true;
    public const bool UsesMockSocketTable = true;
    public const bool QueuesInboundSocketData = true;
    public const bool ExportsPalFunctions = false;
    public const bool OpensLiveSockets = false;
    public const bool PerformsNicIo = false;
    public const bool WaitsOnScheduler = false;
    public const bool StartsKestrel = false;

    public static TcpEstablishedStreamSocketBridgeStatus QueueInboundPayload(
        OSHostSocketTable? table,
        OSHostSocketHandle socket,
        in TcpEstablishedStreamReceiveResult receive,
        ReadOnlySpan<byte> payload,
        out TcpEstablishedStreamSocketBridgeResult result)
    {
        if (!receive.Succeeded)
        {
            result = CreateResult(
                TcpEstablishedStreamSocketBridgeStatus.ReceiveNotSuccessful,
                receive,
                OSHostStatus.InvalidArgument,
                socket,
                payloadBytesQueued: 0);
            return result.Status;
        }

        if (payload.Length != receive.PayloadBytesAccepted)
        {
            result = CreateResult(
                TcpEstablishedStreamSocketBridgeStatus.PayloadLengthMismatch,
                receive,
                OSHostStatus.InvalidArgument,
                socket,
                payloadBytesQueued: 0);
            return result.Status;
        }

        if (table is null)
        {
            result = CreateResult(
                TcpEstablishedStreamSocketBridgeStatus.MissingSocketTable,
                receive,
                OSHostStatus.InvalidArgument,
                socket,
                payloadBytesQueued: 0);
            return result.Status;
        }

        OSHostStatus socketStatus = table.QueueInboundData(socket, payload);
        if (socketStatus != OSHostStatus.Ok)
        {
            result = CreateResult(
                TcpEstablishedStreamSocketBridgeStatus.SocketQueueFailed,
                receive,
                socketStatus,
                socket,
                payloadBytesQueued: 0);
            return result.Status;
        }

        result = CreateResult(
            TcpEstablishedStreamSocketBridgeStatus.Success,
            receive,
            socketStatus,
            socket,
            payload.Length);
        return result.Status;
    }

    private static TcpEstablishedStreamSocketBridgeResult CreateResult(
        TcpEstablishedStreamSocketBridgeStatus status,
        in TcpEstablishedStreamReceiveResult receive,
        OSHostStatus socketStatus,
        OSHostSocketHandle socket,
        int payloadBytesQueued)
    {
        return TcpEstablishedStreamSocketBridgeResult.Create(
            status,
            receive.Status,
            socketStatus,
            socket,
            receive.PayloadBytesAccepted,
            payloadBytesQueued,
            receive.UnreadBytes,
            receive.LocalNextSequenceNumber,
            receive.RemoteNextSequenceNumber);
    }
}
