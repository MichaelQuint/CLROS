using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class IcmpReceiveDispatcherTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.icmp.rx parses echo request", TestParsesEchoRequest, ref failures);
        Run("net.icmp.rx parses echo reply", TestParsesEchoReply, ref failures);
        Run("net.icmp.rx reports classifier failure", TestReportsClassifierFailure, ref failures);
        Run("net.icmp.rx rejects non-ipv4 payload", TestRejectsNonIpv4Payload, ref failures);
        Run("net.icmp.rx rejects non-icmp protocol", TestRejectsNonIcmpProtocol, ref failures);
        Run("net.icmp.rx rejects truncated icmp packet", TestRejectsTruncatedIcmpPacket, ref failures);
        Run("net.icmp.rx rejects bad checksum", TestRejectsBadChecksum, ref failures);
        Run("net.icmp.rx default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "ICMP receive dispatcher host tests failed: " + failures);
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

    private static bool TestParsesEchoRequest()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] buffer = CreateIcmpReceiveBuffer(IcmpEchoPacket.TypeEchoRequest, payload);

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.Success
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.ClassifierPacketStatus == PacketParseStatus.Success
            && result.NetworkPacketStatus == PacketParseStatus.Success
            && result.IcmpPacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.EthernetHeader.EtherType == EthernetHeader.EtherTypeIpv4
            && result.Ipv4Header.Protocol == Ipv4Header.ProtocolIcmp
            && result.SourceAddress == RemoteAddress()
            && result.DestinationAddress == LocalAddress()
            && result.EchoPacket.IsEchoRequest
            && result.EchoPacket.Identifier == 0x1234
            && result.EchoPacket.SequenceNumber == 0x0007
            && result.EchoPayloadOffset == ExpectedIcmpPayloadOffset
            && result.EchoPayloadLength == payload.Length
            && buffer[result.EchoPayloadOffset] == payload[0]
            && buffer[result.EchoPayloadOffset + 3] == payload[3];
    }

    private static bool TestParsesEchoReply()
    {
        byte[] payload = { 0x70, 0x6F, 0x6E, 0x67 };
        byte[] buffer = CreateIcmpReceiveBuffer(IcmpEchoPacket.TypeEchoReply, payload);

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.Success
            && result.IcmpPacketStatus == PacketParseStatus.Success
            && result.EchoPacket.IsEchoReply
            && result.EchoPacket.Identifier == 0x1234
            && result.EchoPacket.SequenceNumber == 0x0007
            && result.EchoPayloadOffset == ExpectedIcmpPayloadOffset
            && result.EchoPayloadLength == payload.Length
            && buffer[result.EchoPayloadOffset] == payload[0]
            && buffer[result.EchoPayloadOffset + 3] == payload[3];
    }

    private static bool TestReportsClassifierFailure()
    {
        byte[] buffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.ClassifierFailed
            && result.NetworkStatus == NetworkReceiveDispatchStatus.ClassifierFailed
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.BufferTooSmall
            && result.ClassifierPacketStatus == PacketParseStatus.BufferTooSmall
            && result.NetworkPacketStatus == PacketParseStatus.BufferTooSmall
            && result.IcmpPacketStatus == PacketParseStatus.BufferTooSmall
            && result.EchoPayloadOffset == 0
            && result.EchoPayloadLength == 0;
    }

    private static bool TestRejectsNonIpv4Payload()
    {
        byte[] buffer = CreateArpReceiveBuffer();

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.NonIpv4Payload
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Arp
            && result.IcmpPacketStatus == PacketParseStatus.UnsupportedEtherType
            && result.EchoPayloadOffset == 0
            && result.EchoPayloadLength == 0;
    }

    private static bool TestRejectsNonIcmpProtocol()
    {
        byte[] buffer = CreateIpv4ReceiveBuffer(Ipv4Header.ProtocolUdp, ipv4PayloadLength: 0);

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.UnsupportedIpProtocol
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.Ipv4Header.Protocol == Ipv4Header.ProtocolUdp
            && result.IcmpPacketStatus == PacketParseStatus.UnsupportedIpProtocol
            && result.EchoPayloadOffset == 0
            && result.EchoPayloadLength == 0;
    }

    private static bool TestRejectsTruncatedIcmpPacket()
    {
        byte[] buffer = CreateIpv4ReceiveBuffer(
            Ipv4Header.ProtocolIcmp,
            IcmpEchoPacket.HeaderLength - 1);

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.IcmpParseFailed
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.Ipv4Header.Protocol == Ipv4Header.ProtocolIcmp
            && result.IcmpPacketStatus == PacketParseStatus.BufferTooSmall
            && result.EchoPayloadOffset == 0
            && result.EchoPayloadLength == 0;
    }

    private static bool TestRejectsBadChecksum()
    {
        byte[] payload = { 0x62, 0x61, 0x64, 0x21 };
        byte[] buffer = CreateIcmpReceiveBuffer(IcmpEchoPacket.TypeEchoRequest, payload);
        buffer[ExpectedIcmpPayloadOffset + 1] ^= 0x01;

        IcmpReceiveDispatchResult result = IcmpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == IcmpReceiveDispatchStatus.IcmpParseFailed
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.IcmpPacketStatus == PacketParseStatus.BadChecksum
            && result.EchoPayloadOffset == 0
            && result.EchoPayloadLength == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        IcmpReceiveDispatchResult defaultResult = default;
        IcmpReceiveDispatchResult success =
            IcmpReceiveDispatcher.Dispatch(
                CreateIcmpReceiveBuffer(IcmpEchoPacket.TypeEchoRequest, payload));
        IcmpReceiveDispatchResult failure =
            IcmpReceiveDispatcher.Dispatch(CreateArpReceiveBuffer());

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == IcmpReceiveDispatchStatus.Success
            && !defaultResult.NetworkResult.IsInitialized
            && defaultResult.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && defaultResult.IcmpPacketStatus == PacketParseStatus.Success
            && defaultResult.EchoPayloadOffset == 0
            && defaultResult.EchoPayloadLength == 0
            && success.IsInitialized
            && success.Succeeded
            && success.NetworkResult.IsInitialized
            && success.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && success.IcmpPacketStatus == PacketParseStatus.Success
            && success.EchoPacket.IsEchoRequest
            && success.EchoPayloadOffset == ExpectedIcmpPayloadOffset
            && success.EchoPayloadLength == payload.Length
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.NetworkResult.IsInitialized
            && failure.Status == IcmpReceiveDispatchStatus.NonIpv4Payload
            && failure.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && failure.PayloadKind == EthernetPayloadKind.Arp
            && failure.IcmpPacketStatus == PacketParseStatus.UnsupportedEtherType
            && failure.EchoPayloadOffset == 0
            && failure.EchoPayloadLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<IcmpReceiveDispatchResult>();
    }

    private static byte[] CreateIcmpReceiveBuffer(byte icmpType, ReadOnlySpan<byte> payload)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            IcmpEchoPacket.HeaderLength +
            payload.Length];

        Ipv4IcmpEchoFrameInfo info = new Ipv4IcmpEchoFrameInfo(
            LocalMac,
            RemoteMac,
            RemoteAddress(),
            LocalAddress(),
            timeToLive: 64,
            identification: 0x2244);

        PacketParseStatus status = Ipv4IcmpEchoFrame.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            info,
            icmpType,
            identifier: 0x1234,
            sequenceNumber: 0x0007,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success
            || bytesWritten != buffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers)
        {
            throw new InvalidOperationException("Failed to build deterministic ICMP receive buffer.");
        }

        return buffer;
    }

    private static byte[] CreateArpReceiveBuffer()
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            ArpPacket.PacketLength];

        EthernetHeader ethernet = new EthernetHeader(
            LocalMac,
            RemoteMac,
            EthernetHeader.EtherTypeArp);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic ARP Ethernet header.");
        }

        ArpPacket packet = new ArpPacket(
            ArpPacket.OperationReply,
            RemoteMac,
            RemoteAddress(),
            LocalMac,
            LocalAddress());
        PacketParseStatus arpStatus = ArpPacket.TryWrite(
            buffer.AsSpan(ExpectedEthernetPayloadOffset, ArpPacket.PacketLength),
            packet,
            out int bytesWritten);
        if (arpStatus != PacketParseStatus.Success || bytesWritten != ArpPacket.PacketLength)
        {
            throw new InvalidOperationException("Failed to build deterministic ARP receive buffer.");
        }

        return buffer;
    }

    private static byte[] CreateIpv4ReceiveBuffer(byte protocol, int ipv4PayloadLength)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            ipv4PayloadLength];

        EthernetHeader ethernet = new EthernetHeader(
            LocalMac,
            RemoteMac,
            EthernetHeader.EtherTypeIpv4);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 Ethernet header.");
        }

        Ipv4Header ipv4Header = new Ipv4Header(
            RemoteAddress(),
            LocalAddress(),
            protocol,
            (ushort)(Ipv4Header.MinHeaderLength + ipv4PayloadLength),
            timeToLive: 64,
            identification: 0x2244,
            flagsAndFragmentOffset: 0,
            typeOfService: 0,
            headerChecksum: 0,
            headerLength: Ipv4Header.MinHeaderLength);

        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            buffer.AsSpan(ExpectedIpv4HeaderOffset, Ipv4Header.MinHeaderLength),
            ipv4Header,
            out int bytesWritten);
        if (ipv4Status != PacketParseStatus.Success || bytesWritten != Ipv4Header.MinHeaderLength)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 header.");
        }

        return buffer;
    }

    private static int ExpectedEthernetPayloadOffset =>
        VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;

    private static int ExpectedIpv4HeaderOffset =>
        VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;

    private static int ExpectedIcmpHeaderOffset =>
        ExpectedIpv4HeaderOffset + Ipv4Header.MinHeaderLength;

    private static int ExpectedIcmpPayloadOffset =>
        ExpectedIcmpHeaderOffset + IcmpEchoPacket.HeaderLength;

    private static MacAddress LocalMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x44);

    private static MacAddress RemoteMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x33);

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 44);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 33);
    }
}
