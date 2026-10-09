using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class DnsResolverTransactionPlannerTests
{
    private const ushort TransactionId = 0x4567;
    private const ushort ClientPort = 49216;
    private const int EncodedExampleComLength = 13;
    private const int ExpectedQueryLength = DnsMessage.HeaderLength + EncodedExampleComLength + 4;
    private const uint ExpectedTtl = 60;

    [HostTest("net.dns.client.transaction writes query frame", HostLane.Fast, 0)]
    private static bool TestWritesQueryFrame()
    {
        DnsResolverTransactionPlanner planner = DnsResolverTransactionPlanner.Create(PlannerInfo());
        byte[] frame = new byte[DnsUdpFrame.QueryFrameHeaderLength + 64];

        DnsResolverTransactionPlanStatus status = planner.TryWriteQueryFrame(
            frame,
            "example.com",
            out DnsResolverTransactionPlanResult result);

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame.AsSpan(0, result.BytesWritten),
            out EthernetIpv4UdpPacketInfo packetInfo,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.Success
            && result.Succeeded
            && result.QueryFrameResult.Succeeded
            && result.BytesWritten == DnsUdpFrame.QueryFrameHeaderLength + ExpectedQueryLength
            && result.QueryNameLength == "example.com".Length
            && result.QueryNameFingerprint != 0
            && result.TransactionId == TransactionId
            && planner.State == DnsResolverState.QueryWritten
            && planner.QueryNameLength == "example.com".Length
            && planner.QueryNameFingerprint == result.QueryNameFingerprint
            && parseStatus == PacketParseStatus.Success
            && packetInfo.SourceAddress == LocalAddress()
            && packetInfo.DestinationAddress == DnsServerAddress()
            && packetInfo.SourcePort == ClientPort
            && packetInfo.DestinationPort == DnsUdpFrame.DnsServerPort
            && payload.Length == ExpectedQueryLength
            && payload[0] == 0x45
            && payload[1] == 0x67
            && payload[^4] == 0x00
            && payload[^3] == 0x01
            && payload[^2] == 0x00
            && payload[^1] == 0x01);
    }

    [HostTest("net.dns.client.transaction default result is not success", HostLane.Fast, 2)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        DnsResolverTransactionPlanResult defaultResult = default;

        DnsResolverTransactionPlanner successPlanner =
            DnsResolverTransactionPlanner.Create(PlannerInfo());
        byte[] successFrame = new byte[DnsUdpFrame.QueryFrameHeaderLength + 64];
        DnsResolverTransactionPlanStatus successStatus = successPlanner.TryWriteQueryFrame(
            successFrame,
            "example.com",
            out DnsResolverTransactionPlanResult success);

        DnsResolverTransactionPlanner failurePlanner =
            DnsResolverTransactionPlanner.Create(PlannerInfo());
        byte[] failureFrame = new byte[DnsUdpFrame.QueryFrameHeaderLength - 1];
        DnsResolverTransactionPlanStatus failureStatus = failurePlanner.TryWriteQueryFrame(
            failureFrame,
            "example.com",
            out DnsResolverTransactionPlanResult failure);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DnsResolverTransactionPlanStatus.Success
            && !defaultResult.QueryFrameResult.IsInitialized
            && !defaultResult.UdpResult.IsInitialized
            && !defaultResult.ReceiveFrameResult.IsInitialized
            && defaultResult.ResolverStatus == DnsResolverStatus.Success
            && defaultResult.MessageStatus == DnsMessageStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.UdpStatus == UdpReceiveDispatchStatus.Success
            && defaultResult.TransactionId == 0
            && defaultResult.QueryNameLength == 0
            && defaultResult.QueryNameFingerprint == 0
            && defaultResult.AnswerAddress == default
            && defaultResult.AnswerTtlSeconds == 0
            && defaultResult.BytesWritten == 0
            && defaultResult.ResponseSize == 0
            && defaultResult.SourceServer == default
            && successStatus == DnsResolverTransactionPlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.QueryFrameResult.IsInitialized
            && success.QueryFrameResult.Succeeded
            && !success.UdpResult.IsInitialized
            && !success.ReceiveFrameResult.IsInitialized
            && success.TransactionId == TransactionId
            && success.QueryNameLength == "example.com".Length
            && success.QueryNameFingerprint != 0
            && success.BytesWritten == DnsUdpFrame.QueryFrameHeaderLength + ExpectedQueryLength
            && successPlanner.State == DnsResolverState.QueryWritten
            && failureStatus == DnsResolverTransactionPlanStatus.QueryFrameFailed
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == DnsResolverTransactionPlanStatus.QueryFrameFailed
            && failure.QueryFrameResult.IsInitialized
            && !failure.QueryFrameResult.Succeeded
            && failure.ResolverStatus == DnsResolverStatus.BufferTooSmall
            && failure.MessageStatus == DnsMessageStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.UdpStatus == UdpReceiveDispatchStatus.Success
            && failure.BytesWritten == 0
            && failure.ResponseSize == 0
            && failurePlanner.State == DnsResolverState.Initial
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DnsResolverTransactionPlanResult>());
    }

    [HostTest("net.dns.client.transaction resolves matching response", HostLane.Fast, 1)]
    private static bool TestResolvesMatchingResponse()
    {
        DnsResolverTransactionPlanner planner = PreparedPlanner();
        byte[] payload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerCount: 1,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            payload);

        DnsResolverTransactionPlanStatus status = planner.ReceiveResponse(
            receiveBuffer,
            out DnsResolverTransactionPlanResult result);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.Success
            && result.Succeeded
            && result.ReceiveFrameResult.Succeeded
            && result.UdpResult.Succeeded
            && result.ResolverStatus == DnsResolverStatus.Success
            && result.MessageStatus == DnsMessageStatus.Success
            && result.AnswerAddress == ResolvedAddress()
            && result.AnswerTtlSeconds == ExpectedTtl
            && result.ResponseSize == payload.Length
            && result.SourceServer == DnsServerAddress()
            && planner.State == DnsResolverState.Resolved
            && planner.ResolvedAddress == ResolvedAddress());
    }

    [HostTest("net.dns.client.transaction rejects wrong transaction", HostLane.Fast, 3)]
    private static bool TestRejectsWrongTransaction()
    {
        DnsResolverTransactionPlanner planner = PreparedPlanner();
        byte[] payload = CreateCompressedResponse(
            TransactionId + 1,
            flags: 0x8180,
            answerCount: 1,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            payload);

        DnsResolverTransactionPlanStatus status = planner.ReceiveResponse(
            receiveBuffer,
            out DnsResolverTransactionPlanResult result);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.WrongTransaction
            && !result.Succeeded
            && result.ResolverStatus == DnsResolverStatus.TransactionIdMismatch
            && result.MessageStatus == DnsMessageStatus.TransactionIdMismatch
            && result.ResponseSize == payload.Length
            && result.SourceServer == DnsServerAddress()
            && planner.State == DnsResolverState.Failed);
    }

    [HostTest("net.dns.client.transaction rejects wrong server", HostLane.Fast, 4)]
    private static bool TestRejectsWrongServer()
    {
        DnsResolverTransactionPlanner planner = PreparedPlanner();
        Ipv4Address wrongServer = new Ipv4Address(203, 0, 113, 53);
        byte[] payload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerCount: 1,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            wrongServer,
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            payload);

        DnsResolverTransactionPlanStatus status = planner.ReceiveResponse(
            receiveBuffer,
            out DnsResolverTransactionPlanResult result);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.WrongServer
            && !result.Succeeded
            && result.UdpResult.Succeeded
            && result.SourceServer == wrongServer
            && result.ResponseSize == payload.Length
            && result.ReceiveFrameResult.Status == default
            && planner.State == DnsResolverState.QueryWritten);
    }

    [HostTest("net.dns.client.transaction rejects wrong udp ports", HostLane.Fast, 5)]
    private static bool TestRejectsWrongUdpPorts()
    {
        DnsResolverTransactionPlanner planner = PreparedPlanner();
        byte[] payload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerCount: 1,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            sourcePort: 5300,
            destinationPort: ClientPort,
            payload);

        DnsResolverTransactionPlanStatus status = planner.ReceiveResponse(
            receiveBuffer,
            out DnsResolverTransactionPlanResult result);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.WrongUdpPorts
            && !result.Succeeded
            && result.UdpResult.Succeeded
            && result.ReceiveFrameResult.Status == DnsUdpFrameStatus.WrongUdpPorts
            && result.SourceServer == DnsServerAddress()
            && planner.State == DnsResolverState.QueryWritten);
    }

    [HostTest("net.dns.client.transaction reports nxdomain and no answer", HostLane.Fast, 6)]
    private static bool TestReportsNxdomainAndNoAnswer()
    {
        DnsResolverTransactionPlanner nxdomainPlanner = PreparedPlanner();
        byte[] nxdomainPayload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8183,
            answerCount: 0,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 0);
        byte[] nxdomainBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            nxdomainPayload);
        DnsResolverTransactionPlanStatus nxdomainStatus = nxdomainPlanner.ReceiveResponse(
            nxdomainBuffer,
            out DnsResolverTransactionPlanResult nxdomainResult);

        DnsResolverTransactionPlanner noAnswerPlanner = PreparedPlanner();
        byte[] noAnswerPayload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerCount: 0,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 0);
        byte[] noAnswerBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            noAnswerPayload);
        DnsResolverTransactionPlanStatus noAnswerStatus = noAnswerPlanner.ReceiveResponse(
            noAnswerBuffer,
            out DnsResolverTransactionPlanResult noAnswerResult);

        return HostCheck.Result(nxdomainStatus == DnsResolverTransactionPlanStatus.Nxdomain
            && nxdomainResult.MessageStatus == DnsMessageStatus.ResponseCodeFailure
            && nxdomainPlanner.State == DnsResolverState.Failed
            && noAnswerStatus == DnsResolverTransactionPlanStatus.NoAnswer
            && noAnswerResult.MessageStatus == DnsMessageStatus.NoAnswer
            && noAnswerPlanner.State == DnsResolverState.Failed);
    }

    [HostTest("net.dns.client.transaction reports malformed response", HostLane.Fast, 7)]
    private static bool TestReportsMalformedResponse()
    {
        DnsResolverTransactionPlanner planner = PreparedPlanner();
        byte[] payload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerCount: 1,
            answerType: DnsMessage.TypeA,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        Array.Resize(ref payload, payload.Length - 1);
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            payload);

        DnsResolverTransactionPlanStatus status = planner.ReceiveResponse(
            receiveBuffer,
            out DnsResolverTransactionPlanResult result);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.MalformedResponse
            && !result.Succeeded
            && result.MessageStatus == DnsMessageStatus.TruncatedAnswer
            && result.ResponseSize == payload.Length
            && planner.State == DnsResolverState.Failed);
    }

    [HostTest("net.dns.client.transaction reports unsupported answer type", HostLane.Fast, 8)]
    private static bool TestReportsUnsupportedAnswerType()
    {
        DnsResolverTransactionPlanner planner = PreparedPlanner();
        byte[] payload = CreateCompressedResponse(
            TransactionId,
            flags: 0x8180,
            answerCount: 1,
            answerType: 28,
            answerClass: DnsMessage.ClassIn,
            answerDataLength: 4);
        byte[] receiveBuffer = CreateResponseReceiveBuffer(
            DnsServerAddress(),
            LocalAddress(),
            DnsUdpFrame.DnsServerPort,
            ClientPort,
            payload);

        DnsResolverTransactionPlanStatus status = planner.ReceiveResponse(
            receiveBuffer,
            out DnsResolverTransactionPlanResult result);

        return HostCheck.Result(status == DnsResolverTransactionPlanStatus.UnsupportedAnswerType
            && !result.Succeeded
            && result.MessageStatus == DnsMessageStatus.UnsupportedType
            && result.ResponseSize == payload.Length
            && planner.State == DnsResolverState.Failed);
    }

    private static DnsResolverTransactionPlanner PreparedPlanner()
    {
        DnsResolverTransactionPlanner planner = DnsResolverTransactionPlanner.Create(PlannerInfo());
        byte[] frame = new byte[DnsUdpFrame.QueryFrameHeaderLength + 64];
        DnsResolverTransactionPlanStatus status = planner.TryWriteQueryFrame(
            frame,
            "example.com",
            out DnsResolverTransactionPlanResult result);

        if (status != DnsResolverTransactionPlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to prepare DNS transaction planner.");
        }

        return planner;
    }

    private static byte[] CreateResponseReceiveBuffer(
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        ReadOnlySpan<byte> payload)
    {
        byte[] receiveBuffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            UdpHeader.HeaderLength +
            payload.Length];

        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            LocalMac,
            NextHopMac,
            sourceAddress,
            destinationAddress,
            sourcePort,
            destinationPort,
            timeToLive: 64,
            identification: 0x5678);

        PacketParseStatus packetStatus = EthernetIpv4UdpPacket.TryWrite(
            receiveBuffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            info,
            payload,
            out int bytesWritten);
        int expectedBytes = receiveBuffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers;
        if (packetStatus != PacketParseStatus.Success || bytesWritten != expectedBytes)
        {
            throw new InvalidOperationException("Failed to build deterministic DNS planner receive buffer.");
        }

        return receiveBuffer;
    }

    private static byte[] CreateCompressedResponse(
        int transactionId,
        ushort flags,
        ushort answerCount,
        ushort answerType,
        ushort answerClass,
        ushort answerDataLength)
    {
        int answerLength = answerCount == 0 ? 0 : 2 + 10 + answerDataLength;
        byte[] response = new byte[
            DnsMessage.HeaderLength +
            EncodedExampleComLength +
            4 +
            answerLength];

        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(0, 2), (ushort)transactionId);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), flags);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), 1);
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(6, 2), answerCount);
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

        if (answerCount == 0)
        {
            return response;
        }

        response[offset++] = 0xC0;
        response[offset++] = 0x0C;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), answerType);
        offset += 2;
        EndianPrimitives.WriteUInt16BigEndian(response.AsSpan(offset, 2), answerClass);
        offset += 2;
        EndianPrimitives.WriteUInt32BigEndian(response.AsSpan(offset, 4), ExpectedTtl);
        offset += 4;
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

    private static DnsResolverTransactionPlannerInfo PlannerInfo()
    {
        return new DnsResolverTransactionPlannerInfo(
            LocalMac,
            NextHopMac,
            LocalAddress(),
            DnsServerAddress(),
            ClientPort,
            TransactionId,
            identification: 0x2345);
    }

    private static MacAddress LocalMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x44);

    private static MacAddress NextHopMac =>
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
