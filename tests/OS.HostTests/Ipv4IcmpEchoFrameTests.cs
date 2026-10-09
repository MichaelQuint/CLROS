using System.Runtime.CompilerServices;
using OS.Net;

internal static class Ipv4IcmpEchoFrameTests
{

    [HostTest("net.icmp.ipv4 frame writes echo request", HostLane.Fast, 0)]
    private static bool TestWritesEchoRequest()
    {
        Ipv4IcmpEchoFrameInfo info = CreateRequestInfo();
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67, 0x21 };
        byte[] frame = new byte[Ipv4IcmpEchoFrame.HeaderLength + payload.Length];

        PacketParseStatus status = Ipv4IcmpEchoFrame.TryWriteEchoRequest(
            frame,
            info,
            identifier: 0x1234,
            sequenceNumber: 0x0007,
            payload,
            out int bytesWritten);

        byte[] expected =
        {
            0x02, 0x00, 0x00, 0x00, 0x00, 0x02,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x00,
            0x45, 0x00, 0x00, 0x21,
            0x12, 0x34, 0x00, 0x00,
            0x40, 0x01, 0x7C, 0x56,
            0xC0, 0x00, 0x02, 0x0A,
            0xC6, 0x33, 0x64, 0x14,
            0x08, 0x00, 0xE5, 0xF3,
            0x12, 0x34, 0x00, 0x07,
            0x70, 0x69, 0x6E, 0x67, 0x21
        };

        return HostCheck.Result(status == PacketParseStatus.Success
            && bytesWritten == expected.Length
            && frame.AsSpan(0, bytesWritten).SequenceEqual(expected));
    }

    [HostTest("net.icmp.ipv4 frame parses echo reply", HostLane.Fast, 1)]
    private static bool TestParsesEchoReply()
    {
        Ipv4IcmpEchoFrameInfo info = CreateReplyInfo();
        byte[] payload = { 0x4F, 0x53, 0x5F, 0x4F, 0x4B };
        byte[] frame = new byte[Ipv4IcmpEchoFrame.HeaderLength + payload.Length];

        PacketParseStatus writeStatus = Ipv4IcmpEchoFrame.TryWriteEchoReply(
            frame,
            info,
            identifier: 0xBEEF,
            sequenceNumber: 0x0102,
            payload,
            out int bytesWritten);

        PacketParseStatus parseStatus = Ipv4IcmpEchoFrame.TryParse(
            frame.AsSpan(0, bytesWritten),
            out Ipv4IcmpEchoFrameInfo parsedInfo,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> parsedPayload);

        return HostCheck.Result(writeStatus == PacketParseStatus.Success
            && bytesWritten == frame.Length
            && parseStatus == PacketParseStatus.Success
            && parsedInfo.DestinationMac == info.DestinationMac
            && parsedInfo.SourceMac == info.SourceMac
            && parsedInfo.SourceAddress == info.SourceAddress
            && parsedInfo.DestinationAddress == info.DestinationAddress
            && parsedInfo.TimeToLive == info.TimeToLive
            && parsedInfo.Identification == info.Identification
            && parsedInfo.FlagsAndFragmentOffset == info.FlagsAndFragmentOffset
            && echo.IsEchoReply
            && !echo.IsEchoRequest
            && echo.Identifier == 0xBEEF
            && echo.SequenceNumber == 0x0102
            && parsedPayload.SequenceEqual(payload));
    }

    [HostTest("net.icmp.ipv4 frame rejects bad ip checksum", HostLane.Fast, 2)]
    private static bool TestRejectsBadIpChecksum()
    {
        byte[] frame = CreateReplyFrame();
        frame[EthernetHeader.HeaderLength + 8] ^= 0x01;

        PacketParseStatus status = Ipv4IcmpEchoFrame.TryParse(
            frame,
            out Ipv4IcmpEchoFrameInfo info,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(status == PacketParseStatus.BadChecksum
            && info.TimeToLive == 0
            && echo.Type == 0
            && payload.Length == 0);
    }

    [HostTest("net.icmp.ipv4 frame rejects truncated payload", HostLane.Fast, 3)]
    private static bool TestRejectsTruncatedPayload()
    {
        byte[] frame = CreateReplyFrame();

        PacketParseStatus status = Ipv4IcmpEchoFrame.TryParse(
            frame.AsSpan(0, frame.Length - 1),
            out Ipv4IcmpEchoFrameInfo info,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(status == PacketParseStatus.TruncatedPayload
            && info.TimeToLive == 0
            && echo.Type == 0
            && payload.Length == 0);
    }

    private static Ipv4IcmpEchoFrameInfo CreateRequestInfo()
    {
        return new Ipv4IcmpEchoFrameInfo(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            new Ipv4Address(192, 0, 2, 10),
            new Ipv4Address(198, 51, 100, 20),
            timeToLive: 64,
            identification: 0x1234);
    }

    private static Ipv4IcmpEchoFrameInfo CreateReplyInfo()
    {
        return new Ipv4IcmpEchoFrameInfo(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02),
            new Ipv4Address(198, 51, 100, 20),
            new Ipv4Address(192, 0, 2, 10),
            timeToLive: 64,
            identification: 0x4321);
    }

    private static byte[] CreateReplyFrame()
    {
        Ipv4IcmpEchoFrameInfo info = CreateReplyInfo();
        byte[] payload = { 0x4F, 0x53, 0x5F, 0x4F, 0x4B };
        byte[] frame = new byte[Ipv4IcmpEchoFrame.HeaderLength + payload.Length];
        PacketParseStatus status = Ipv4IcmpEchoFrame.TryWriteEchoReply(
            frame,
            info,
            identifier: 0xBEEF,
            sequenceNumber: 0x0102,
            payload,
            out int bytesWritten);

        if (status != PacketParseStatus.Success || bytesWritten != frame.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 ICMP echo reply frame.");
        }

        return frame;
    }
}
