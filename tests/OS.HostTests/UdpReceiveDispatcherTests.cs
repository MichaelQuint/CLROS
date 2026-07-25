using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class UdpReceiveDispatcherTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.udp.rx parses udp payload", TestParsesUdpPayload, ref failures);
        Run("net.udp.rx accepts zero checksum", TestAcceptsZeroChecksum, ref failures);
        Run("net.udp.rx validates nonzero checksum", TestValidatesNonZeroChecksum, ref failures);
        Run("net.udp.rx rejects bad checksum", TestRejectsBadChecksum, ref failures);
        Run("net.udp.rx reports classifier failure", TestReportsClassifierFailure, ref failures);
        Run("net.udp.rx rejects non-ipv4 payload", TestRejectsNonIpv4Payload, ref failures);
        Run("net.udp.rx rejects non-udp protocol", TestRejectsNonUdpProtocol, ref failures);
        Run("net.udp.rx reports truncated udp header", TestReportsTruncatedUdpHeader, ref failures);
        Run("net.udp.rx reports truncated udp payload", TestReportsTruncatedUdpPayload, ref failures);
        Run("net.udp.rx default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "UDP receive dispatcher host tests failed: " + failures);
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

    private static bool TestParsesUdpPayload()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        byte[] buffer = CreateUdpReceiveBuffer(payload, writeChecksum: false);

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.Success
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.ClassifierPacketStatus == PacketParseStatus.Success
            && result.NetworkPacketStatus == PacketParseStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.EthernetHeader.EtherType == EthernetHeader.EtherTypeIpv4
            && result.SourceAddress == RemoteAddress()
            && result.DestinationAddress == LocalAddress()
            && result.SourcePort == 53000
            && result.DestinationPort == 67
            && result.UdpHeader.Length == UdpHeader.HeaderLength + payload.Length
            && result.UdpPayloadOffset == ExpectedUdpPayloadOffset
            && result.UdpPayloadLength == payload.Length
            && buffer[result.UdpPayloadOffset] == payload[0]
            && buffer[result.UdpPayloadOffset + 3] == payload[3];
    }

    private static bool TestAcceptsZeroChecksum()
    {
        byte[] payload = { 0x7A, 0x65, 0x72, 0x6F };
        byte[] buffer = CreateUdpReceiveBuffer(payload, writeChecksum: false);

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.Success
            && result.UdpHeader.Checksum == 0
            && result.UdpPayloadLength == payload.Length
            && buffer[result.UdpPayloadOffset] == payload[0]
            && buffer[result.UdpPayloadOffset + 3] == payload[3];
    }

    private static bool TestValidatesNonZeroChecksum()
    {
        byte[] payload = { 0x76, 0x61, 0x6C, 0x69, 0x64 };
        byte[] buffer = CreateUdpReceiveBuffer(payload, writeChecksum: true);

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.Success
            && result.UdpHeader.Checksum != 0
            && result.UdpPayloadLength == payload.Length
            && buffer[result.UdpPayloadOffset] == payload[0]
            && buffer[result.UdpPayloadOffset + 4] == payload[4];
    }

    private static bool TestRejectsBadChecksum()
    {
        byte[] payload = { 0x62, 0x61, 0x64, 0x21 };
        byte[] buffer = CreateUdpReceiveBuffer(payload, writeChecksum: true);
        buffer[ExpectedUdpPayloadOffset + 1] ^= 0x01;

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.BadUdpChecksum
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.NetworkPacketStatus == PacketParseStatus.Success
            && result.UdpPacketStatus == PacketParseStatus.BadChecksum
            && result.UdpPayloadOffset == 0
            && result.UdpPayloadLength == 0;
    }

    private static bool TestReportsClassifierFailure()
    {
        byte[] buffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.ClassifierFailed
            && result.NetworkStatus == NetworkReceiveDispatchStatus.ClassifierFailed
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.BufferTooSmall
            && result.ClassifierPacketStatus == PacketParseStatus.BufferTooSmall
            && result.NetworkPacketStatus == PacketParseStatus.BufferTooSmall
            && result.UdpPacketStatus == PacketParseStatus.BufferTooSmall
            && result.UdpPayloadOffset == 0
            && result.UdpPayloadLength == 0;
    }

    private static bool TestRejectsNonIpv4Payload()
    {
        byte[] buffer = CreateArpReceiveBuffer();

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.NonIpv4Payload
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Arp
            && result.UdpPacketStatus == PacketParseStatus.UnsupportedEtherType
            && result.UdpPayloadOffset == 0
            && result.UdpPayloadLength == 0;
    }

    private static bool TestRejectsNonUdpProtocol()
    {
        byte[] buffer = CreateIpv4ReceiveBuffer(protocol: 1, ipv4PayloadLength: 0);

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.UnsupportedIpProtocol
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.Ipv4Header.Protocol == 1
            && result.UdpPacketStatus == PacketParseStatus.UnsupportedIpProtocol
            && result.UdpPayloadOffset == 0
            && result.UdpPayloadLength == 0;
    }

    private static bool TestReportsTruncatedUdpHeader()
    {
        byte[] buffer = CreateIpv4ReceiveBuffer(
            Ipv4Header.ProtocolUdp,
            UdpHeader.HeaderLength - 1);

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.UdpParseFailed
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.Ipv4Header.Protocol == Ipv4Header.ProtocolUdp
            && result.UdpPacketStatus == PacketParseStatus.BufferTooSmall
            && result.UdpPayloadOffset == 0
            && result.UdpPayloadLength == 0;
    }

    private static bool TestReportsTruncatedUdpPayload()
    {
        const int ActualUdpBytes = UdpHeader.HeaderLength + 2;
        byte[] buffer = CreateIpv4ReceiveBuffer(
            Ipv4Header.ProtocolUdp,
            ActualUdpBytes);

        Span<byte> udp = buffer.AsSpan(ExpectedUdpHeaderOffset, ActualUdpBytes);
        EndianPrimitives.WriteUInt16BigEndian(udp.Slice(0, 2), 53000);
        EndianPrimitives.WriteUInt16BigEndian(udp.Slice(2, 2), 67);
        EndianPrimitives.WriteUInt16BigEndian(
            udp.Slice(4, 2),
            UdpHeader.HeaderLength + 4);
        EndianPrimitives.WriteUInt16BigEndian(udp.Slice(6, 2), 0);
        udp[UdpHeader.HeaderLength] = 0xAA;
        udp[UdpHeader.HeaderLength + 1] = 0xBB;

        UdpReceiveDispatchResult result = UdpReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == UdpReceiveDispatchStatus.UdpParseFailed
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.Ipv4Header.Protocol == Ipv4Header.ProtocolUdp
            && result.Ipv4Header.TotalLength == Ipv4Header.MinHeaderLength + ActualUdpBytes
            && result.UdpPacketStatus == PacketParseStatus.TruncatedPayload
            && result.UdpPayloadOffset == 0
            && result.UdpPayloadLength == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        UdpReceiveDispatchResult defaultResult = default;
        UdpReceiveDispatchResult success =
            UdpReceiveDispatcher.Dispatch(CreateUdpReceiveBuffer(payload, writeChecksum: false));
        UdpReceiveDispatchResult failure =
            UdpReceiveDispatcher.Dispatch(CreateArpReceiveBuffer());

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == UdpReceiveDispatchStatus.Success
            && !defaultResult.NetworkResult.IsInitialized
            && defaultResult.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && defaultResult.UdpPacketStatus == PacketParseStatus.Success
            && defaultResult.UdpPayloadOffset == 0
            && defaultResult.UdpPayloadLength == 0
            && success.IsInitialized
            && success.Succeeded
            && success.NetworkResult.IsInitialized
            && success.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && success.UdpPacketStatus == PacketParseStatus.Success
            && success.SourcePort == 53000
            && success.DestinationPort == 67
            && success.UdpPayloadOffset == ExpectedUdpPayloadOffset
            && success.UdpPayloadLength == payload.Length
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.NetworkResult.IsInitialized
            && failure.Status == UdpReceiveDispatchStatus.NonIpv4Payload
            && failure.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && failure.PayloadKind == EthernetPayloadKind.Arp
            && failure.UdpPacketStatus == PacketParseStatus.UnsupportedEtherType
            && failure.UdpPayloadOffset == 0
            && failure.UdpPayloadLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<UdpReceiveDispatchResult>();
    }

    private static byte[] CreateUdpReceiveBuffer(ReadOnlySpan<byte> payload, bool writeChecksum = false)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            UdpHeader.HeaderLength +
            payload.Length];

        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            LocalMac,
            RemoteMac,
            RemoteAddress(),
            LocalAddress(),
            sourcePort: 53000,
            destinationPort: 67,
            timeToLive: 64,
            identification: 0x2244);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            info,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success
            || bytesWritten != buffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers)
        {
            throw new InvalidOperationException("Failed to build deterministic UDP receive buffer.");
        }

        Span<byte> udpDatagram = buffer.AsSpan(
            ExpectedUdpHeaderOffset,
            UdpHeader.HeaderLength + payload.Length);
        if (!writeChecksum)
        {
            EndianPrimitives.WriteUInt16BigEndian(udpDatagram.Slice(6, sizeof(ushort)), 0);
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

    private static int ExpectedUdpHeaderOffset =>
        ExpectedIpv4HeaderOffset + Ipv4Header.MinHeaderLength;

    private static int ExpectedUdpPayloadOffset =>
        ExpectedUdpHeaderOffset + UdpHeader.HeaderLength;

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
