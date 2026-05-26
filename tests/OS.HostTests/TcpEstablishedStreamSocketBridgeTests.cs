using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;
using OS.Pal;

internal static class TcpEstablishedStreamSocketBridgeTests
{
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 50100;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp.stream.socketbridge queues inbound payload", TestQueuesInboundPayload, ref failures);
        Run("net.tcp.stream.socketbridge rejects failed receive result", TestRejectsFailedReceiveResult, ref failures);
        Run("net.tcp.stream.socketbridge rejects default receive result", TestRejectsDefaultReceiveResult, ref failures);
        Run("net.tcp.stream.socketbridge rejects payload length mismatch", TestRejectsPayloadLengthMismatch, ref failures);
        Run("net.tcp.stream.socketbridge propagates mock socket failure", TestPropagatesMockSocketFailure, ref failures);
        Run("net.tcp.stream.socketbridge preserves mock-only boundary", TestPreservesMockOnlyBoundary, ref failures);
        Run("net.tcp.stream.socketbridge default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP established stream socket bridge host tests failed: " + failures);
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

    private static bool TestRejectsDefaultReceiveResult()
    {
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketHandle socket = CreateAcceptedSocket(table);
        TcpEstablishedStreamReceiveResult receive = default;

        TcpEstablishedStreamSocketBridgeStatus status =
            TcpEstablishedStreamSocketBridge.QueueInboundPayload(
                table,
                socket,
                receive,
                ReadOnlySpan<byte>.Empty,
                out TcpEstablishedStreamSocketBridgeResult result);

        Span<byte> destination = stackalloc byte[1];
        OSHostSocketOperationResult read = table.Receive(socket, destination);

        return status == TcpEstablishedStreamSocketBridgeStatus.ReceiveNotSuccessful
            && !result.Succeeded
            && result.ReceiveStatus == TcpEstablishedStreamStatus.Success
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && result.PayloadBytesAccepted == 0
            && result.PayloadBytesQueued == 0
            && read.StatusCode == (int)OSHostStatus.WouldBlock;
    }

    private static bool TestQueuesInboundPayload()
    {
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        TcpEstablishedStreamReceiveResult receive = ReceivePayload(payload);
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketHandle socket = CreateAcceptedSocket(table);

        TcpEstablishedStreamSocketBridgeStatus status =
            TcpEstablishedStreamSocketBridge.QueueInboundPayload(
                table,
                socket,
                receive,
                payload,
                out TcpEstablishedStreamSocketBridgeResult result);

        Span<byte> destination = stackalloc byte[5];
        OSHostSocketOperationResult read = table.Receive(socket, destination);

        return status == TcpEstablishedStreamSocketBridgeStatus.Success
            && result.Succeeded
            && result.ReceiveStatus == TcpEstablishedStreamStatus.Success
            && result.SocketStatus == OSHostStatus.Ok
            && result.Socket.Value == socket.Value
            && result.PayloadBytesAccepted == payload.Length
            && result.PayloadBytesQueued == payload.Length
            && result.UnreadBytes == payload.Length
            && result.LocalNextSequenceNumber == LocalNextSequence
            && result.RemoteNextSequenceNumber == RemoteNextSequence + (uint)payload.Length
            && read.StatusCode == (int)OSHostStatus.Ok
            && read.BytesTransferred == (ulong)payload.Length
            && BytesEqual(destination, payload);
    }

    private static bool TestRejectsFailedReceiveResult()
    {
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketHandle socket = CreateAcceptedSocket(table);
        TcpEstablishedStreamReceiveResult failed = TcpEstablishedStreamReceiveResult.Fail(
            TcpEstablishedStreamStatus.OutOfOrderSegment,
            PacketParseStatus.Success,
            unreadBytes: 0,
            LocalNextSequence,
            RemoteNextSequence);
        byte[] payload = Encoding.ASCII.GetBytes("abc");

        TcpEstablishedStreamSocketBridgeStatus status =
            TcpEstablishedStreamSocketBridge.QueueInboundPayload(
                table,
                socket,
                failed,
                payload,
                out TcpEstablishedStreamSocketBridgeResult result);

        Span<byte> destination = stackalloc byte[3];
        OSHostSocketOperationResult read = table.Receive(socket, destination);

        return status == TcpEstablishedStreamSocketBridgeStatus.ReceiveNotSuccessful
            && !result.Succeeded
            && result.ReceiveStatus == TcpEstablishedStreamStatus.OutOfOrderSegment
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && result.PayloadBytesAccepted == 0
            && result.PayloadBytesQueued == 0
            && read.StatusCode == (int)OSHostStatus.WouldBlock;
    }

    private static bool TestRejectsPayloadLengthMismatch()
    {
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        TcpEstablishedStreamReceiveResult receive = ReceivePayload(payload);
        OSHostSocketTable table = new(capacity: 2);
        OSHostSocketHandle socket = CreateAcceptedSocket(table);

        TcpEstablishedStreamSocketBridgeStatus status =
            TcpEstablishedStreamSocketBridge.QueueInboundPayload(
                table,
                socket,
                receive,
                payload.AsSpan(0, payload.Length - 1),
                out TcpEstablishedStreamSocketBridgeResult result);

        Span<byte> destination = stackalloc byte[5];
        OSHostSocketOperationResult read = table.Receive(socket, destination);

        return status == TcpEstablishedStreamSocketBridgeStatus.PayloadLengthMismatch
            && !result.Succeeded
            && result.ReceiveStatus == TcpEstablishedStreamStatus.Success
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && result.PayloadBytesAccepted == payload.Length
            && result.PayloadBytesQueued == 0
            && read.StatusCode == (int)OSHostStatus.WouldBlock;
    }

    private static bool TestPropagatesMockSocketFailure()
    {
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        TcpEstablishedStreamReceiveResult receive = ReceivePayload(payload);
        OSHostSocketTable table = new(capacity: 1);
        OSHostSocketCreateResult created = table.Create(TcpCreateOptions());

        TcpEstablishedStreamSocketBridgeStatus status =
            TcpEstablishedStreamSocketBridge.QueueInboundPayload(
                table,
                created.Socket,
                receive,
                payload,
                out TcpEstablishedStreamSocketBridgeResult result);

        return created.Succeeded
            && status == TcpEstablishedStreamSocketBridgeStatus.SocketQueueFailed
            && !result.Succeeded
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && result.PayloadBytesAccepted == payload.Length
            && result.PayloadBytesQueued == 0;
    }

    private static bool TestPreservesMockOnlyBoundary()
    {
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        TcpEstablishedStreamReceiveResult receive = ReceivePayload(payload);
        TcpEstablishedStreamSocketBridgeStatus missingTable =
            TcpEstablishedStreamSocketBridge.QueueInboundPayload(
                table: null,
                new OSHostSocketHandle(1),
                receive,
                payload,
                out TcpEstablishedStreamSocketBridgeResult result);

        return TcpEstablishedStreamSocketBridge.UsesTcpReceiveResult
            && TcpEstablishedStreamSocketBridge.UsesCallerOwnedPayload
            && TcpEstablishedStreamSocketBridge.UsesMockSocketTable
            && TcpEstablishedStreamSocketBridge.QueuesInboundSocketData
            && !TcpEstablishedStreamSocketBridge.ExportsPalFunctions
            && !TcpEstablishedStreamSocketBridge.OpensLiveSockets
            && !TcpEstablishedStreamSocketBridge.PerformsNicIo
            && !TcpEstablishedStreamSocketBridge.WaitsOnScheduler
            && !TcpEstablishedStreamSocketBridge.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpEstablishedStreamSocketBridgeResult>()
            && missingTable == TcpEstablishedStreamSocketBridgeStatus.MissingSocketTable
            && result.SocketStatus == OSHostStatus.InvalidArgument
            && result.PayloadBytesQueued == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpEstablishedStreamSocketBridgeResult defaultResult = default;
        OSHostSocketHandle socket = new OSHostSocketHandle(7);
        TcpEstablishedStreamSocketBridgeResult failureResult =
            TcpEstablishedStreamSocketBridgeResult.Create(
                TcpEstablishedStreamSocketBridgeStatus.ReceiveNotSuccessful,
                TcpEstablishedStreamStatus.OutOfOrderSegment,
                OSHostStatus.InvalidArgument,
                socket,
                payloadBytesAccepted: 0,
                payloadBytesQueued: 0,
                unreadBytes: 0,
                LocalNextSequence,
                RemoteNextSequence);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpEstablishedStreamSocketBridgeStatus.Success
            && failureResult.IsInitialized
            && !failureResult.Succeeded
            && failureResult.Status == TcpEstablishedStreamSocketBridgeStatus.ReceiveNotSuccessful
            && failureResult.ReceiveStatus == TcpEstablishedStreamStatus.OutOfOrderSegment
            && failureResult.SocketStatus == OSHostStatus.InvalidArgument
            && failureResult.Socket.Value == socket.Value
            && failureResult.PayloadBytesAccepted == 0
            && failureResult.PayloadBytesQueued == 0
            && failureResult.UnreadBytes == 0
            && failureResult.LocalNextSequenceNumber == LocalNextSequence
            && failureResult.RemoteNextSequenceNumber == RemoteNextSequence
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpEstablishedStreamSocketBridgeResult>();
    }

    private static TcpEstablishedStreamReceiveResult ReceivePayload(ReadOnlySpan<byte> payload)
    {
        Span<byte> receiveBuffer = stackalloc byte[32];
        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength + 32];
        int segmentBytes = WriteRemoteSegment(segment, payload);
        if (stream.ReceiveSegment(
                segment.Slice(0, segmentBytes),
                out TcpEstablishedStreamReceiveResult receive) != TcpEstablishedStreamStatus.Success)
        {
            throw new InvalidOperationException("Failed to create receive fixture.");
        }

        return receive;
    }

    private static int WriteRemoteSegment(
        Span<byte> segment,
        ReadOnlySpan<byte> payload)
    {
        PacketParseStatus status = TcpHeader.TryWrite(
            segment,
            RemotePort,
            LocalPort,
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            windowSize: 0x2000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            payload,
            out int bytesWritten);

        return status == PacketParseStatus.Success ? bytesWritten : 0;
    }

    private static OSHostSocketHandle CreateAcceptedSocket(OSHostSocketTable table)
    {
        OSHostSocketCreateResult create = table.Create(TcpCreateOptions());
        if (!create.Succeeded
            || table.Bind(create.Socket, Endpoint(0, 0, 0, 0, LocalPort)) != OSHostStatus.Ok
            || table.Listen(create.Socket, backlog: 1) != OSHostStatus.Ok
            || table.EnqueueAcceptedConnection(
                create.Socket,
                Endpoint(192, 0, 2, 10, LocalPort),
                Endpoint(198, 51, 100, 20, RemotePort)) != OSHostStatus.Ok)
        {
            throw new InvalidOperationException("Failed to create accepted socket fixture.");
        }

        OSHostSocketAcceptResult accepted = table.Accept(create.Socket);
        if (!accepted.Succeeded)
        {
            throw new InvalidOperationException("Failed to accept socket fixture.");
        }

        return accepted.Socket;
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

    private static bool BytesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
