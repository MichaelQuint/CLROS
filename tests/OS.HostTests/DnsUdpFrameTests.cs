using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class DnsUdpFrameTests
{
    private const ushort TransactionId = 0x3456;
    private const ushort ClientPort = 49152;
    private const int EncodedExampleComLength = 13;
    private const int ExpectedQueryLength = DnsMessage.HeaderLength + EncodedExampleComLength + 4;

    [HostTest("net.dns.udp writes query frame", HostLane.Fast, 0)]
    private static bool TestWritesQueryFrame()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        byte[] frame = new byte[DnsUdpFrame.QueryFrameHeaderLength + 64];

        DnsUdpQueryFrameResult result = DnsUdpFrame.TryWriteQueryFrame(
            frame,
            QueryInfo(),
            "example.com",
            ref transaction);

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame.AsSpan(0, result.BytesWritten),
            out EthernetIpv4UdpPacketInfo packetInfo,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(result.Succeeded
            && result.Status == DnsUdpFrameStatus.Success
            && result.ResolverStatus == DnsResolverStatus.Success
            && result.MessageStatus == DnsMessageStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.ResolverResult.State == DnsResolverState.QueryWritten
            && result.BytesWritten == DnsUdpFrame.QueryFrameHeaderLength + ExpectedQueryLength
            && result.DnsPayloadOffset == DnsUdpFrame.QueryFrameHeaderLength
            && result.DnsPayloadLength == ExpectedQueryLength
            && transaction.State == DnsResolverState.QueryWritten
            && parseStatus == PacketParseStatus.Success
            && packetInfo.DestinationMac == DestinationMac
            && packetInfo.SourceMac == LocalMac
            && packetInfo.SourceAddress == LocalAddress()
            && packetInfo.DestinationAddress == DnsServerAddress()
            && packetInfo.SourcePort == ClientPort
            && packetInfo.DestinationPort == DnsUdpFrame.DnsServerPort
            && payload.Length == ExpectedQueryLength
            && payload[0] == 0x34
            && payload[1] == 0x56
            && payload[2] == 0x01
            && payload[3] == 0x00
            && payload[^4] == 0x00
            && payload[^3] == 0x01
            && payload[^2] == 0x00
            && payload[^1] == 0x01);
    }

    [HostTest("net.dns.udp query default result is not success", HostLane.Fast, 1)]
    private static bool TestQueryDefaultResultIsNotSuccess()
    {
        DnsUdpQueryFrameResult defaultResult = default;

        DnsResolverTransaction successTransaction = DnsResolverTransaction.Create(TransactionId);
        byte[] successFrame = new byte[DnsUdpFrame.QueryFrameHeaderLength + 64];
        DnsUdpQueryFrameResult success = DnsUdpFrame.TryWriteQueryFrame(
            successFrame,
            QueryInfo(),
            "example.com",
            ref successTransaction);

        DnsResolverTransaction failureTransaction = DnsResolverTransaction.Create(TransactionId);
        byte[] failureFrame = new byte[DnsUdpFrame.QueryFrameHeaderLength - 1];
        DnsUdpQueryFrameResult failure = DnsUdpFrame.TryWriteQueryFrame(
            failureFrame,
            QueryInfo(),
            "example.com",
            ref failureTransaction);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DnsUdpFrameStatus.Success
            && defaultResult.ResolverStatus == DnsResolverStatus.Success
            && defaultResult.MessageStatus == DnsMessageStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && !defaultResult.ResolverResult.IsInitialized
            && defaultResult.BytesWritten == 0
            && defaultResult.DnsPayloadOffset == 0
            && defaultResult.DnsPayloadLength == 0
            && success.IsInitialized
            && success.Succeeded
            && success.Status == DnsUdpFrameStatus.Success
            && success.ResolverResult.IsInitialized
            && success.ResolverResult.State == DnsResolverState.QueryWritten
            && success.BytesWritten == DnsUdpFrame.QueryFrameHeaderLength + ExpectedQueryLength
            && success.DnsPayloadOffset == DnsUdpFrame.QueryFrameHeaderLength
            && success.DnsPayloadLength == ExpectedQueryLength
            && successTransaction.State == DnsResolverState.QueryWritten
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == DnsUdpFrameStatus.BufferTooSmall
            && failure.ResolverStatus == DnsResolverStatus.BufferTooSmall
            && failure.MessageStatus == DnsMessageStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && !failure.ResolverResult.IsInitialized
            && failure.BytesWritten == 0
            && failure.DnsPayloadOffset == 0
            && failure.DnsPayloadLength == 0
            && failureTransaction.State == DnsResolverState.Initial
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DnsUdpQueryFrameResult>());
    }

    [HostTest("net.dns.udp resolves response payload", HostLane.Fast, 2)]
    private static bool TestResolvesResponsePayload()
    {
        DnsResolverTransaction transaction = PreparedTransaction();
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            DnsMessage.TypeA,
            TransactionId);

        DnsUdpReceiveFrameResult result = DnsUdpFrame.ReceiveResponse(
            receiveBuffer,
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            ref transaction);

        return HostCheck.Result(result.Succeeded
            && result.Status == DnsUdpFrameStatus.Success
            && result.UdpStatus == UdpReceiveDispatchStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.ResolverInvoked
            && result.ResolverStatus == DnsResolverStatus.Success
            && result.MessageStatus == DnsMessageStatus.Success
            && result.ResolverResult.State == DnsResolverState.Resolved
            && result.ResolvedAddress == ResolvedAddress()
            && result.SourcePort == DnsUdpFrame.DnsServerPort
            && result.DestinationPort == ClientPort
            && result.DnsPayloadLength > DnsMessage.HeaderLength
            && transaction.State == DnsResolverState.Resolved
            && transaction.ResolvedAddress == ResolvedAddress());
    }

    [HostTest("net.dns.udp receive default result is not success", HostLane.Fast, 3)]
    private static bool TestReceiveDefaultResultIsNotSuccess()
    {
        DnsUdpReceiveFrameResult defaultResult = default;

        DnsResolverTransaction successTransaction = PreparedTransaction();
        byte[] successBuffer = CreateResponseReceiveBuffer(
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            DnsMessage.TypeA,
            TransactionId);

        DnsUdpReceiveFrameResult success = DnsUdpFrame.ReceiveResponse(
            successBuffer,
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            ref successTransaction);

        DnsResolverTransaction failureTransaction = PreparedTransaction();
        byte[] failureBuffer = CreateResponseReceiveBuffer(
            sourcePort: 5300,
            destinationPort: ClientPort,
            DnsMessage.TypeA,
            TransactionId);

        DnsUdpReceiveFrameResult failure = DnsUdpFrame.ReceiveResponse(
            failureBuffer,
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            ref failureTransaction);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DnsUdpFrameStatus.Success
            && !defaultResult.UdpResult.IsInitialized
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.ResolverStatus == DnsResolverStatus.Success
            && defaultResult.MessageStatus == DnsMessageStatus.Success
            && !defaultResult.ResolverResult.IsInitialized
            && !defaultResult.ResolverInvoked
            && defaultResult.SourcePort == 0
            && defaultResult.DestinationPort == 0
            && defaultResult.DnsPayloadOffset == 0
            && defaultResult.DnsPayloadLength == 0
            && success.IsInitialized
            && success.Succeeded
            && success.Status == DnsUdpFrameStatus.Success
            && success.UdpResult.IsInitialized
            && success.ResolverResult.IsInitialized
            && success.ResolverInvoked
            && success.DnsPayloadLength > DnsMessage.HeaderLength
            && success.ResolvedAddress == ResolvedAddress()
            && successTransaction.State == DnsResolverState.Resolved
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == DnsUdpFrameStatus.WrongUdpPorts
            && failure.UdpResult.IsInitialized
            && failure.PacketStatus == PacketParseStatus.Success
            && !failure.ResolverInvoked
            && failure.SourcePort == 5300
            && failure.DestinationPort == ClientPort
            && failureTransaction.State == DnsResolverState.QueryWritten
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DnsUdpReceiveFrameResult>());
    }

    [HostTest("net.dns.udp rejects wrong udp ports", HostLane.Fast, 4)]
    private static bool TestRejectsWrongUdpPorts()
    {
        DnsResolverTransaction transaction = PreparedTransaction();
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            sourcePort: 5300,
            destinationPort: ClientPort,
            DnsMessage.TypeA,
            TransactionId);

        DnsUdpReceiveFrameResult result = DnsUdpFrame.ReceiveResponse(
            receiveBuffer,
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            ref transaction);

        return HostCheck.Result(!result.Succeeded
            && result.Status == DnsUdpFrameStatus.WrongUdpPorts
            && result.UdpStatus == UdpReceiveDispatchStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && !result.ResolverInvoked
            && result.SourcePort == 5300
            && result.DestinationPort == ClientPort
            && transaction.State == DnsResolverState.QueryWritten);
    }

    [HostTest("net.dns.udp reports resolver parse failure", HostLane.Fast, 5)]
    private static bool TestReportsResolverParseFailure()
    {
        DnsResolverTransaction transaction = PreparedTransaction();
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            answerType: 28,
            TransactionId);

        DnsUdpReceiveFrameResult result = DnsUdpFrame.ReceiveResponse(
            receiveBuffer,
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            ref transaction);

        return HostCheck.Result(!result.Succeeded
            && result.Status == DnsUdpFrameStatus.ResolverParseFailed
            && result.UdpStatus == UdpReceiveDispatchStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.ResolverInvoked
            && result.ResolverStatus == DnsResolverStatus.MessageParseFailed
            && result.MessageStatus == DnsMessageStatus.UnsupportedType
            && result.ResolverResult.State == DnsResolverState.Failed
            && transaction.State == DnsResolverState.Failed);
    }

    [HostTest("net.dns.udp reports short frame buffer", HostLane.Fast, 6)]
    private static bool TestReportsShortFrameBuffer()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        byte[] frame = new byte[DnsUdpFrame.QueryFrameHeaderLength - 1];

        DnsUdpQueryFrameResult result = DnsUdpFrame.TryWriteQueryFrame(
            frame,
            QueryInfo(),
            "example.com",
            ref transaction);

        return HostCheck.Result(!result.Succeeded
            && result.Status == DnsUdpFrameStatus.BufferTooSmall
            && result.ResolverStatus == DnsResolverStatus.BufferTooSmall
            && result.MessageStatus == DnsMessageStatus.BufferTooSmall
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.BytesWritten == 0
            && result.DnsPayloadOffset == 0
            && result.DnsPayloadLength == 0
            && transaction.State == DnsResolverState.Initial);
    }

    private static DnsResolverTransaction PreparedTransaction()
    {
        DnsResolverTransaction transaction = DnsResolverTransaction.Create(TransactionId);
        byte[] frame = new byte[DnsUdpFrame.QueryFrameHeaderLength + 64];
        DnsUdpQueryFrameResult result = DnsUdpFrame.TryWriteQueryFrame(
            frame,
            QueryInfo(),
            "example.com",
            ref transaction);

        if (!result.Succeeded || transaction.State != DnsResolverState.QueryWritten)
        {
            throw new InvalidOperationException("Failed to prepare DNS resolver transaction.");
        }

        return transaction;
    }

    private static byte[] CreateResponseReceiveBuffer(
        ushort sourcePort,
        ushort destinationPort,
        ushort answerType,
        ushort transactionId)
    {
        byte[] dnsPayload = CreateCompressedResponse(
            transactionId,
            flags: 0x8180,
            answerType,
            DnsMessage.ClassIn,
            answerDataLength: 4);
        byte[] receiveBuffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            UdpHeader.HeaderLength +
            dnsPayload.Length];

        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            LocalMac,
            DestinationMac,
            DnsServerAddress(),
            LocalAddress(),
            sourcePort,
            destinationPort,
            timeToLive: 64,
            identification: 0x4567);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(
            receiveBuffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            info,
            dnsPayload,
            out int bytesWritten);
        int expectedBytes = receiveBuffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers;
        if (status != PacketParseStatus.Success || bytesWritten != expectedBytes)
        {
            throw new InvalidOperationException("Failed to build deterministic DNS UDP receive buffer.");
        }

        return receiveBuffer;
    }

    private static byte[] CreateCompressedResponse(
        ushort transactionId,
        ushort flags,
        ushort answerType,
        ushort answerClass,
        ushort answerDataLength)
    {
        byte[] response = new byte[
            DnsMessage.HeaderLength +
            EncodedExampleComLength +
            4 +
            2 +
            10 +
            answerDataLength];

        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(0, 2), transactionId);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), flags);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), 1);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(6, 2), 1);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(8, 2), 0);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(10, 2), 0);

        int offset = DnsMessage.HeaderLength;
        response[offset++] = 0x07;
        response[offset++] = 0x65;
        response[offset++] = 0x78;
        response[offset++] = 0x61;
        response[offset++] = 0x6D;
        response[offset++] = 0x70;
        response[offset++] = 0x6C;
        response[offset++] = 0x65;
        response[offset++] = 0x03;
        response[offset++] = 0x63;
        response[offset++] = 0x6F;
        response[offset++] = 0x6D;
        response[offset++] = 0x00;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), DnsMessage.TypeA);
        offset += 2;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), DnsMessage.ClassIn);
        offset += 2;

        response[offset++] = 0xC0;
        response[offset++] = 0x0C;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), answerType);
        offset += 2;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), answerClass);
        offset += 2;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x00;
        response[offset++] = 0x3C;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), answerDataLength);
        offset += 2;

        if (answerDataLength > 0)
        {
            response[offset++] = 0x5D;
        }

        if (answerDataLength > 1)
        {
            response[offset++] = 0xB8;
        }

        if (answerDataLength > 2)
        {
            response[offset++] = 0xD8;
        }

        if (answerDataLength > 3)
        {
            response[offset] = 0x22;
        }

        return response;
    }

    private static DnsUdpQueryFrameInfo QueryInfo()
    {
        return new DnsUdpQueryFrameInfo(
            LocalMac,
            DestinationMac,
            LocalAddress(),
            DnsServerAddress(),
            ClientPort,
            identification: 0x1234);
    }

    private static MacAddress LocalMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x44);

    private static MacAddress DestinationMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x53);

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 44);
    }

    private static Ipv4Address DnsServerAddress()
    {
        return new Ipv4Address(198, 51, 100, 53);
    }

    private static Ipv4Address ResolvedAddress()
    {
        return new Ipv4Address(93, 184, 216, 34);
    }
}
