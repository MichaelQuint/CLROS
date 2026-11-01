using OS.Pal;

namespace OS.Net;
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
    public static TcpEstablishedStreamSocketBridgeStatus QueueInboundPayload(OSHostSocketTable? table, OSHostSocketHandle socket, in TcpEstablishedStreamReceiveResult receive, ReadOnlySpan<byte> payload, out TcpEstablishedStreamSocketBridgeResult result)
    {
        if (!receive.Succeeded)
        {
            result = CreateResult(TcpEstablishedStreamSocketBridgeStatus.ReceiveNotSuccessful, receive, OSHostStatus.InvalidArgument, socket, payloadBytesQueued: 0);
            return result.Status;
        }

        if (payload.Length != receive.PayloadBytesAccepted)
        {
            result = CreateResult(TcpEstablishedStreamSocketBridgeStatus.PayloadLengthMismatch, receive, OSHostStatus.InvalidArgument, socket, payloadBytesQueued: 0);
            return result.Status;
        }

        if (table is null)
        {
            result = CreateResult(TcpEstablishedStreamSocketBridgeStatus.MissingSocketTable, receive, OSHostStatus.InvalidArgument, socket, payloadBytesQueued: 0);
            return result.Status;
        }

        OSHostStatus socketStatus = table.QueueInboundData(socket, payload);
        if (socketStatus != OSHostStatus.Ok)
        {
            result = CreateResult(TcpEstablishedStreamSocketBridgeStatus.SocketQueueFailed, receive, socketStatus, socket, payloadBytesQueued: 0);
            return result.Status;
        }

        result = CreateResult(TcpEstablishedStreamSocketBridgeStatus.Success, receive, socketStatus, socket, payload.Length);
        return result.Status;
    }

    private static TcpEstablishedStreamSocketBridgeResult CreateResult(TcpEstablishedStreamSocketBridgeStatus status, in TcpEstablishedStreamReceiveResult receive, OSHostStatus socketStatus, OSHostSocketHandle socket, int payloadBytesQueued)
    {
        return TcpEstablishedStreamSocketBridgeResult.Create(status, receive.Status, socketStatus, socket, receive.PayloadBytesAccepted, payloadBytesQueued, receive.UnreadBytes, receive.LocalNextSequenceNumber, receive.RemoteNextSequenceNumber);
    }
}
