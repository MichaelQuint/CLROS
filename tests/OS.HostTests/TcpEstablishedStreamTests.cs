using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class TcpEstablishedStreamTests
{
    private const ushort LocalPort = 49170;
    private const ushort RemotePort = 8080;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp stream receives in-order payload", TestReceivesInOrderPayload, ref failures);
        Run("net.tcp stream preserves unread bytes", TestPreservesUnreadBytes, ref failures);
        Run("net.tcp stream rejects out-of-order payload", TestRejectsOutOfOrderPayload, ref failures);
        Run("net.tcp stream rejects wrong acknowledgement", TestRejectsWrongAcknowledgement, ref failures);
        Run("net.tcp stream writes segmented payload", TestWritesSegmentedPayload, ref failures);
        Run("net.tcp stream receive default result is not success", TestDefaultReceiveResultIsNotSuccess, ref failures);
        Run("net.tcp stream read default result is not success", TestDefaultReadResultIsNotSuccess, ref failures);
        Run("net.tcp stream send default result is not success", TestDefaultSendResultIsNotSuccess, ref failures);
        Run("net.tcp stream uses shared memory copy helper", TestUsesSharedMemoryCopyHelper, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP established stream host tests failed: " + failures);
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

    private static bool TestDefaultSendResultIsNotSuccess()
    {
        TcpEstablishedStreamSendResult result = default;
        TcpEstablishedStreamSendResult failed = TcpEstablishedStreamSendResult.Fail(
            TcpEstablishedStreamStatus.NoPayload,
            PacketParseStatus.Success,
            sequenceNumber: LocalNextSequence,
            acknowledgementNumber: RemoteNextSequence,
            localNextSequenceNumber: LocalNextSequence,
            remoteNextSequenceNumber: RemoteNextSequence);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == TcpEstablishedStreamStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.SegmentBytesWritten == 0
            && result.PayloadBytesWritten == 0
            && result.SequenceNumber == 0
            && result.AcknowledgementNumber == 0
            && result.LocalNextSequenceNumber == 0
            && result.RemoteNextSequenceNumber == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == TcpEstablishedStreamStatus.NoPayload
            && failed.SequenceNumber == LocalNextSequence
            && failed.AcknowledgementNumber == RemoteNextSequence
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpEstablishedStreamSendResult>();
    }

    private static bool TestDefaultReadResultIsNotSuccess()
    {
        TcpEstablishedStreamReadResult result = default;
        TcpEstablishedStreamReadResult failed = TcpEstablishedStreamReadResult.Fail(
            TcpEstablishedStreamStatus.NoData,
            unreadBytes: 0,
            RemoteNextSequence);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == TcpEstablishedStreamStatus.Success
            && result.BytesRead == 0
            && result.UnreadBytes == 0
            && result.AcknowledgementNumber == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == TcpEstablishedStreamStatus.NoData
            && failed.AcknowledgementNumber == RemoteNextSequence
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpEstablishedStreamReadResult>();
    }

    private static bool TestDefaultReceiveResultIsNotSuccess()
    {
        TcpEstablishedStreamReceiveResult result = default;
        TcpEstablishedStreamReceiveResult failed = TcpEstablishedStreamReceiveResult.Fail(
            TcpEstablishedStreamStatus.NoPayload,
            PacketParseStatus.Success,
            unreadBytes: 0,
            LocalNextSequence,
            RemoteNextSequence);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == TcpEstablishedStreamStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.PayloadBytesAccepted == 0
            && result.UnreadBytes == 0
            && result.LocalNextSequenceNumber == 0
            && result.RemoteNextSequenceNumber == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == TcpEstablishedStreamStatus.NoPayload
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpEstablishedStreamReceiveResult>();
    }

    private static bool TestReceivesInOrderPayload()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength + 5];
        int segmentBytes = WriteRemoteSegment(segment, RemoteNextSequence, payload);

        TcpEstablishedStreamStatus receiveStatus = stream.ReceiveSegment(
            segment.Slice(0, segmentBytes),
            out TcpEstablishedStreamReceiveResult receiveResult);
        if (receiveStatus != TcpEstablishedStreamStatus.Success
            || !receiveResult.Succeeded
            || receiveResult.PayloadBytesAccepted != 5
            || receiveResult.UnreadBytes != 5
            || receiveResult.LocalNextSequenceNumber != LocalNextSequence
            || receiveResult.RemoteNextSequenceNumber != RemoteNextSequence + 5
            || stream.UnreadBytes != 5
            || stream.AcknowledgementNumber != RemoteNextSequence + 5)
        {
            return false;
        }

        Span<byte> readBuffer = stackalloc byte[5];
        TcpEstablishedStreamStatus readStatus = stream.Read(
            readBuffer,
            out TcpEstablishedStreamReadResult readResult);

        return readStatus == TcpEstablishedStreamStatus.Success
            && readResult.Succeeded
            && readResult.BytesRead == 5
            && readResult.UnreadBytes == 0
            && readResult.AcknowledgementNumber == RemoteNextSequence + 5
            && BytesEqual(readBuffer, payload)
            && stream.UnreadBytes == 0
            && stream.RemoteNextSequenceNumber == RemoteNextSequence + 5;
    }

    private static bool TestPreservesUnreadBytes()
    {
        Span<byte> receiveBuffer = stackalloc byte[8];
        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
        byte[] firstPayload = Encoding.ASCII.GetBytes("abcdef");
        Span<byte> firstSegment = stackalloc byte[TcpHeader.MinHeaderLength + 6];
        int firstSegmentBytes = WriteRemoteSegment(firstSegment, RemoteNextSequence, firstPayload);

        if (stream.ReceiveSegment(
                firstSegment.Slice(0, firstSegmentBytes),
                out TcpEstablishedStreamReceiveResult firstReceive) != TcpEstablishedStreamStatus.Success
            || firstReceive.UnreadBytes != 6)
        {
            return false;
        }

        Span<byte> partialRead = stackalloc byte[2];
        if (stream.Read(partialRead, out TcpEstablishedStreamReadResult partialReadResult)
                != TcpEstablishedStreamStatus.Success
            || partialReadResult.BytesRead != 2
            || partialReadResult.UnreadBytes != 4
            || !BytesEqual(partialRead, Encoding.ASCII.GetBytes("ab")))
        {
            return false;
        }

        byte[] secondPayload = Encoding.ASCII.GetBytes("gh");
        Span<byte> secondSegment = stackalloc byte[TcpHeader.MinHeaderLength + 2];
        int secondSegmentBytes = WriteRemoteSegment(
            secondSegment,
            RemoteNextSequence + 6,
            secondPayload);
        if (stream.ReceiveSegment(
                secondSegment.Slice(0, secondSegmentBytes),
                out TcpEstablishedStreamReceiveResult secondReceive) != TcpEstablishedStreamStatus.Success
            || secondReceive.PayloadBytesAccepted != 2
            || secondReceive.UnreadBytes != 6
            || stream.AcknowledgementNumber != RemoteNextSequence + 8)
        {
            return false;
        }

        Span<byte> finalRead = stackalloc byte[6];
        TcpEstablishedStreamStatus finalReadStatus = stream.Read(
            finalRead,
            out TcpEstablishedStreamReadResult finalReadResult);

        return finalReadStatus == TcpEstablishedStreamStatus.Success
            && finalReadResult.BytesRead == 6
            && finalReadResult.UnreadBytes == 0
            && BytesEqual(finalRead, Encoding.ASCII.GetBytes("cdefgh"))
            && stream.UnreadBytes == 0;
    }

    private static bool TestRejectsOutOfOrderPayload()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
        byte[] payload = Encoding.ASCII.GetBytes("abc");
        Span<byte> outOfOrderSegment = stackalloc byte[TcpHeader.MinHeaderLength + 3];
        int outOfOrderBytes = WriteRemoteSegment(
            outOfOrderSegment,
            RemoteNextSequence + 3,
            payload);

        TcpEstablishedStreamStatus outOfOrderStatus = stream.ReceiveSegment(
            outOfOrderSegment.Slice(0, outOfOrderBytes),
            out TcpEstablishedStreamReceiveResult outOfOrderResult);
        if (outOfOrderStatus != TcpEstablishedStreamStatus.OutOfOrderSegment
            || outOfOrderResult.Succeeded
            || outOfOrderResult.UnreadBytes != 0
            || stream.AcknowledgementNumber != RemoteNextSequence
            || stream.UnreadBytes != 0)
        {
            return false;
        }

        Span<byte> inOrderSegment = stackalloc byte[TcpHeader.MinHeaderLength + 3];
        int inOrderBytes = WriteRemoteSegment(inOrderSegment, RemoteNextSequence, payload);
        if (stream.ReceiveSegment(
                inOrderSegment.Slice(0, inOrderBytes),
                out TcpEstablishedStreamReceiveResult inOrderResult) != TcpEstablishedStreamStatus.Success
            || inOrderResult.UnreadBytes != 3
            || stream.AcknowledgementNumber != RemoteNextSequence + 3)
        {
            return false;
        }

        Span<byte> duplicateSegment = stackalloc byte[TcpHeader.MinHeaderLength + 3];
        int duplicateBytes = WriteRemoteSegment(duplicateSegment, RemoteNextSequence, payload);
        TcpEstablishedStreamStatus duplicateStatus = stream.ReceiveSegment(
            duplicateSegment.Slice(0, duplicateBytes),
            out TcpEstablishedStreamReceiveResult duplicateResult);

        return duplicateStatus == TcpEstablishedStreamStatus.DuplicateSegment
            && !duplicateResult.Succeeded
            && duplicateResult.UnreadBytes == 3
            && stream.AcknowledgementNumber == RemoteNextSequence + 3
            && stream.UnreadBytes == 3;
    }

    private static bool TestRejectsWrongAcknowledgement()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
        byte[] payload = Encoding.ASCII.GetBytes("abc");
        Span<byte> wrongAckSegment = stackalloc byte[TcpHeader.MinHeaderLength + 3];
        int wrongAckBytes = WriteRemoteSegment(
            wrongAckSegment,
            RemoteNextSequence,
            payload,
            LocalNextSequence + 1);

        TcpEstablishedStreamStatus wrongAckStatus = stream.ReceiveSegment(
            wrongAckSegment.Slice(0, wrongAckBytes),
            out TcpEstablishedStreamReceiveResult wrongAckResult);
        if (wrongAckStatus != TcpEstablishedStreamStatus.InvalidAcknowledgementNumber
            || wrongAckResult.Succeeded
            || wrongAckResult.PayloadBytesAccepted != 0
            || wrongAckResult.UnreadBytes != 0
            || wrongAckResult.LocalNextSequenceNumber != LocalNextSequence
            || wrongAckResult.RemoteNextSequenceNumber != RemoteNextSequence
            || stream.LocalNextSequenceNumber != LocalNextSequence
            || stream.AcknowledgementNumber != RemoteNextSequence
            || stream.UnreadBytes != 0)
        {
            return false;
        }

        Span<byte> correctSegment = stackalloc byte[TcpHeader.MinHeaderLength + 3];
        int correctBytes = WriteRemoteSegment(correctSegment, RemoteNextSequence, payload);
        TcpEstablishedStreamStatus correctStatus = stream.ReceiveSegment(
            correctSegment.Slice(0, correctBytes),
            out TcpEstablishedStreamReceiveResult correctResult);

        return correctStatus == TcpEstablishedStreamStatus.Success
            && correctResult.Succeeded
            && correctResult.PayloadBytesAccepted == 3
            && correctResult.UnreadBytes == 3
            && stream.AcknowledgementNumber == RemoteNextSequence + 3;
    }

    private static bool TestWritesSegmentedPayload()
    {
        Span<byte> receiveBuffer = stackalloc byte[8];
        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
        byte[] payload = Encoding.ASCII.GetBytes("abcdefghij");
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength + 4];
        int payloadOffset = 0;
        int segmentsWritten = 0;

        while (payloadOffset < payload.Length)
        {
            TcpEstablishedStreamStatus status = stream.TryWritePayloadSegment(
                payload.AsSpan(payloadOffset),
                maximumPayloadLength: 4,
                segment,
                windowSize: 0x4000,
                checksum: 0,
                out TcpEstablishedStreamSendResult result);
            if (status != TcpEstablishedStreamStatus.Success
                || !result.Succeeded
                || result.PayloadBytesWritten <= 0
                || result.PayloadBytesWritten > 4
                || result.SegmentBytesWritten != TcpHeader.MinHeaderLength + result.PayloadBytesWritten
                || result.SequenceNumber != LocalNextSequence + (uint)payloadOffset
                || result.AcknowledgementNumber != RemoteNextSequence
                || result.LocalNextSequenceNumber != LocalNextSequence + (uint)(payloadOffset + result.PayloadBytesWritten))
            {
                return false;
            }

            if (!ParseOutboundPayload(
                    segment.Slice(0, result.SegmentBytesWritten),
                    result.SequenceNumber,
                    RemoteNextSequence,
                    payload.AsSpan(payloadOffset, result.PayloadBytesWritten)))
            {
                return false;
            }

            payloadOffset += result.PayloadBytesWritten;
            segmentsWritten++;
        }

        return segmentsWritten == 3
            && payloadOffset == payload.Length
            && stream.LocalNextSequenceNumber == LocalNextSequence + 10
            && stream.RemoteNextSequenceNumber == RemoteNextSequence;
    }

    private static bool TestUsesSharedMemoryCopyHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Net",
            "TcpEstablishedStream.cs"));

        return source.Contains("OS.Std.MemoryExtensions.TryCopyTo(source, destination)", StringComparison.Ordinal)
            && !source.Contains("destination[index] = source[index];", StringComparison.Ordinal);
    }

    private static int WriteRemoteSegment(
        Span<byte> segment,
        uint sequenceNumber,
        ReadOnlySpan<byte> payload,
        uint acknowledgementNumber = LocalNextSequence)
    {
        PacketParseStatus status = TcpHeader.TryWrite(
            segment,
            RemotePort,
            LocalPort,
            sequenceNumber,
            acknowledgementNumber,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            windowSize: 0x2000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            payload,
            out int bytesWritten);

        return status == PacketParseStatus.Success ? bytesWritten : 0;
    }

    private static bool ParseOutboundPayload(
        ReadOnlySpan<byte> segment,
        uint expectedSequenceNumber,
        uint expectedAcknowledgementNumber,
        ReadOnlySpan<byte> expectedPayload)
    {
        return TcpHeader.TryParse(
                segment,
                out TcpHeader header,
                out ReadOnlySpan<byte> options,
                out ReadOnlySpan<byte> payload) == PacketParseStatus.Success
            && header.SourcePort == LocalPort
            && header.DestinationPort == RemotePort
            && header.SequenceNumber == expectedSequenceNumber
            && header.AcknowledgementNumber == expectedAcknowledgementNumber
            && header.Flags == (TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck)
            && options.Length == 0
            && BytesEqual(payload, expectedPayload);
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
