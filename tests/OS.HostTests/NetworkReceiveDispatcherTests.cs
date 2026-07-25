using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;

internal static class NetworkReceiveDispatcherTests
{
    private const ushort UnsupportedEtherType = 0x86DD;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.rx.dispatch parses arp payload", TestParsesArpPayload, ref failures);
        Run("net.rx.dispatch parses ipv4 payload", TestParsesIpv4Payload, ref failures);
        Run("net.rx.dispatch reports classifier failure", TestReportsClassifierFailure, ref failures);
        Run("net.rx.dispatch reports arp parse failure", TestReportsArpParseFailure, ref failures);
        Run("net.rx.dispatch reports ipv4 parse failure", TestReportsIpv4ParseFailure, ref failures);
        Run("net.rx.dispatch reports unsupported payload", TestReportsUnsupportedPayload, ref failures);
        Run("net.rx.dispatch default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Network receive dispatcher host tests failed: " + failures);
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

    private static bool TestParsesArpPayload()
    {
        byte[] buffer = CreateArpReceiveBuffer();

        NetworkReceiveDispatchResult result =
            NetworkReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == NetworkReceiveDispatchStatus.Success
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.ClassifierPacketStatus == PacketParseStatus.Success
            && result.PayloadPacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Arp
            && result.EthernetHeader.EtherType == EthernetHeader.EtherTypeArp
            && result.EthernetHeader.Destination == DestinationMac
            && result.EthernetHeader.Source == SourceMac
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == ArpPacket.PacketLength
            && result.ArpPacket.Operation == ArpPacket.OperationRequest
            && result.ArpPacket.SenderMac == SourceMac
            && result.ArpPacket.SenderAddress == LocalAddress()
            && result.ArpPacket.TargetMac == default
            && result.ArpPacket.TargetAddress == TargetAddress()
            && result.Ipv4PayloadOffset == 0
            && result.Ipv4PayloadLength == 0;
    }

    private static bool TestParsesIpv4Payload()
    {
        byte[] buffer = CreateIpv4UdpReceiveBuffer();

        NetworkReceiveDispatchResult result =
            NetworkReceiveDispatcher.Dispatch(buffer);

        return result.Succeeded
            && result.Status == NetworkReceiveDispatchStatus.Success
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.ClassifierPacketStatus == PacketParseStatus.Success
            && result.PayloadPacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.EthernetHeader.EtherType == EthernetHeader.EtherTypeIpv4
            && result.Ipv4Header.Source == LocalAddress()
            && result.Ipv4Header.Destination == RemoteAddress()
            && result.Ipv4Header.Protocol == Ipv4Header.ProtocolUdp
            && result.Ipv4Header.TotalLength == Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + 4
            && result.Ipv4PayloadOffset == ExpectedPayloadOffset + Ipv4Header.MinHeaderLength
            && result.Ipv4PayloadLength == UdpHeader.HeaderLength + 4;
    }

    private static bool TestReportsClassifierFailure()
    {
        byte[] buffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];

        NetworkReceiveDispatchResult result =
            NetworkReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == NetworkReceiveDispatchStatus.ClassifierFailed
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.BufferTooSmall
            && result.ClassifierPacketStatus == PacketParseStatus.BufferTooSmall
            && result.PayloadPacketStatus == PacketParseStatus.BufferTooSmall
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.PayloadOffset == 0
            && result.PayloadLength == 0;
    }

    private static bool TestReportsArpParseFailure()
    {
        byte[] buffer = CreateReceiveBuffer(
            EthernetHeader.EtherTypeArp,
            ArpPacket.PacketLength - 1);

        NetworkReceiveDispatchResult result =
            NetworkReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == NetworkReceiveDispatchStatus.ArpParseFailed
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.ClassifierPacketStatus == PacketParseStatus.Success
            && result.PayloadPacketStatus == PacketParseStatus.BufferTooSmall
            && result.PayloadKind == EthernetPayloadKind.Arp
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == ArpPacket.PacketLength - 1;
    }

    private static bool TestReportsIpv4ParseFailure()
    {
        byte[] buffer = CreateReceiveBuffer(
            EthernetHeader.EtherTypeIpv4,
            Ipv4Header.MinHeaderLength - 1);

        NetworkReceiveDispatchResult result =
            NetworkReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == NetworkReceiveDispatchStatus.Ipv4ParseFailed
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.ClassifierPacketStatus == PacketParseStatus.Success
            && result.PayloadPacketStatus == PacketParseStatus.BufferTooSmall
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == Ipv4Header.MinHeaderLength - 1;
    }

    private static bool TestReportsUnsupportedPayload()
    {
        byte[] buffer = CreateReceiveBuffer(UnsupportedEtherType, payloadBytes: 8);

        NetworkReceiveDispatchResult result =
            NetworkReceiveDispatcher.Dispatch(buffer);

        return !result.Succeeded
            && result.Status == NetworkReceiveDispatchStatus.UnsupportedPayload
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.UnsupportedPayload
            && result.ClassifierPacketStatus == PacketParseStatus.UnsupportedEtherType
            && result.PayloadPacketStatus == PacketParseStatus.UnsupportedEtherType
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.EthernetHeader.EtherType == UnsupportedEtherType
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == 8;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NetworkReceiveDispatchResult defaultResult = default;
        NetworkReceiveDispatchResult arpResult =
            NetworkReceiveDispatcher.Dispatch(CreateArpReceiveBuffer());
        NetworkReceiveDispatchResult ipv4Result =
            NetworkReceiveDispatcher.Dispatch(CreateIpv4UdpReceiveBuffer());
        NetworkReceiveDispatchResult failure =
            NetworkReceiveDispatcher.Dispatch(CreateReceiveBuffer(UnsupportedEtherType, payloadBytes: 8));

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == NetworkReceiveDispatchStatus.Success
            && defaultResult.PayloadPacketStatus == PacketParseStatus.Success
            && defaultResult.PayloadOffset == 0
            && defaultResult.PayloadLength == 0
            && defaultResult.Ipv4PayloadOffset == 0
            && defaultResult.Ipv4PayloadLength == 0
            && arpResult.IsInitialized
            && arpResult.Succeeded
            && arpResult.PayloadKind == EthernetPayloadKind.Arp
            && arpResult.ArpPacket.SenderMac == SourceMac
            && arpResult.PayloadOffset == ExpectedPayloadOffset
            && arpResult.PayloadLength == ArpPacket.PacketLength
            && ipv4Result.IsInitialized
            && ipv4Result.Succeeded
            && ipv4Result.PayloadKind == EthernetPayloadKind.Ipv4
            && ipv4Result.Ipv4Header.Protocol == Ipv4Header.ProtocolUdp
            && ipv4Result.Ipv4PayloadOffset == ExpectedPayloadOffset + Ipv4Header.MinHeaderLength
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == NetworkReceiveDispatchStatus.UnsupportedPayload
            && failure.PayloadPacketStatus == PacketParseStatus.UnsupportedEtherType
            && failure.PayloadKind == EthernetPayloadKind.Unsupported
            && failure.PayloadOffset == ExpectedPayloadOffset
            && failure.PayloadLength == 8
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NetworkReceiveDispatchResult>();
    }

    private static byte[] CreateArpReceiveBuffer()
    {
        byte[] buffer = CreateReceiveBuffer(
            EthernetHeader.EtherTypeArp,
            ArpPacket.PacketLength);
        ArpPacket packet = new ArpPacket(
            ArpPacket.OperationRequest,
            SourceMac,
            LocalAddress(),
            default,
            TargetAddress());

        PacketParseStatus status = ArpPacket.TryWrite(
            buffer.AsSpan(ExpectedPayloadOffset, ArpPacket.PacketLength),
            packet,
            out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != ArpPacket.PacketLength)
        {
            throw new InvalidOperationException("Failed to build deterministic ARP receive buffer.");
        }

        return buffer;
    }

    private static byte[] CreateIpv4UdpReceiveBuffer()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            UdpHeader.HeaderLength +
            payload.Length];

        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            DestinationMac,
            SourceMac,
            LocalAddress(),
            RemoteAddress(),
            sourcePort: 12345,
            destinationPort: 8080,
            timeToLive: 64,
            identification: 0x1234);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            info,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success
            || bytesWritten != buffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 receive buffer.");
        }

        return buffer;
    }

    private static byte[] CreateReceiveBuffer(ushort etherType, int payloadBytes)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            payloadBytes];

        EthernetHeader ethernet = new EthernetHeader(
            DestinationMac,
            SourceMac,
            etherType);

        PacketParseStatus status = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernet);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet receive buffer.");
        }

        return buffer;
    }

    private static int ExpectedPayloadOffset =>
        VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;

    private static MacAddress DestinationMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x22);

    private static MacAddress SourceMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x11);

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 10);
    }

    private static Ipv4Address TargetAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 20);
    }
}
