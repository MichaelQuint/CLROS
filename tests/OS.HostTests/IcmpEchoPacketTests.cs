using System.Runtime.CompilerServices;
using OS.Net;

internal static class IcmpEchoPacketTests
{

    [HostTest("net.icmp echo request round trip", HostLane.Fast, 0)]
    private static bool TestEchoRequestRoundTrip()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67, 0x21 };
        byte[] packet = new byte[IcmpEchoPacket.HeaderLength + payload.Length];

        PacketParseStatus writeStatus = IcmpEchoPacket.TryWriteEchoRequest(
            packet,
            identifier: 0x1234,
            sequenceNumber: 0x0007,
            payload,
            out int bytesWritten);

        byte[] expected =
        {
            0x08, 0x00, 0xE5, 0xF3,
            0x12, 0x34, 0x00, 0x07,
            0x70, 0x69, 0x6E, 0x67, 0x21
        };

        PacketParseStatus parseStatus = IcmpEchoPacket.TryParse(
            packet,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> parsedPayload);

        return HostCheck.Result(writeStatus == PacketParseStatus.Success
            && bytesWritten == expected.Length
            && packet.AsSpan(0, bytesWritten).SequenceEqual(expected)
            && parseStatus == PacketParseStatus.Success
            && echo.IsEchoRequest
            && !echo.IsEchoReply
            && echo.Type == IcmpEchoPacket.TypeEchoRequest
            && echo.Code == IcmpEchoPacket.EchoCode
            && echo.Checksum == 0xE5F3
            && echo.Identifier == 0x1234
            && echo.SequenceNumber == 0x0007
            && parsedPayload.SequenceEqual(payload));
    }

    [HostTest("net.icmp echo reply round trip", HostLane.Fast, 1)]
    private static bool TestEchoReplyRoundTrip()
    {
        byte[] payload = { 0x4F, 0x53, 0x5F, 0x4F, 0x4B };
        byte[] packet = new byte[IcmpEchoPacket.HeaderLength + payload.Length];

        PacketParseStatus writeStatus = IcmpEchoPacket.TryWriteEchoReply(
            packet,
            identifier: 0xBEEF,
            sequenceNumber: 0x0102,
            payload,
            out int bytesWritten);

        byte[] expected =
        {
            0x00, 0x00, 0x46, 0x6B,
            0xBE, 0xEF, 0x01, 0x02,
            0x4F, 0x53, 0x5F, 0x4F, 0x4B
        };

        PacketParseStatus parseStatus = IcmpEchoPacket.TryParse(
            packet,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> parsedPayload);

        return HostCheck.Result(writeStatus == PacketParseStatus.Success
            && bytesWritten == expected.Length
            && packet.AsSpan(0, bytesWritten).SequenceEqual(expected)
            && parseStatus == PacketParseStatus.Success
            && !echo.IsEchoRequest
            && echo.IsEchoReply
            && echo.Type == IcmpEchoPacket.TypeEchoReply
            && echo.Code == IcmpEchoPacket.EchoCode
            && echo.Checksum == 0x466B
            && echo.Identifier == 0xBEEF
            && echo.SequenceNumber == 0x0102
            && parsedPayload.SequenceEqual(payload));
    }

    [HostTest("net.icmp rejects bad checksum", HostLane.Fast, 2)]
    private static bool TestRejectsBadChecksum()
    {
        byte[] packet = CreateEchoRequest();
        packet[packet.Length - 1] ^= 0x01;

        PacketParseStatus status = IcmpEchoPacket.TryParse(
            packet,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(status == PacketParseStatus.BadChecksum
            && echo.Type == 0
            && payload.Length == 0);
    }

    [HostTest("net.icmp rejects short packet", HostLane.Fast, 3)]
    private static bool TestRejectsShortPacket()
    {
        byte[] packet = new byte[IcmpEchoPacket.HeaderLength - 1];

        PacketParseStatus status = IcmpEchoPacket.TryParse(
            packet,
            out IcmpEchoPacket echo,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(status == PacketParseStatus.BufferTooSmall
            && echo.Type == 0
            && payload.Length == 0);
    }

    [HostTest("net.icmp rejects unsupported type code", HostLane.Fast, 4)]
    private static bool TestRejectsUnsupportedTypeAndCode()
    {
        byte[] unsupportedType = CreateEchoRequest();
        unsupportedType[0] = 3;
        WriteChecksum(unsupportedType);

        byte[] unsupportedCode = CreateEchoRequest();
        unsupportedCode[1] = 1;
        WriteChecksum(unsupportedCode);

        PacketParseStatus parseTypeStatus = IcmpEchoPacket.TryParse(
            unsupportedType,
            out IcmpEchoPacket _,
            out ReadOnlySpan<byte> _);
        PacketParseStatus parseCodeStatus = IcmpEchoPacket.TryParse(
            unsupportedCode,
            out IcmpEchoPacket _,
            out ReadOnlySpan<byte> _);
        PacketParseStatus writeTypeStatus = IcmpEchoPacket.TryWrite(
            new byte[IcmpEchoPacket.HeaderLength],
            type: 3,
            code: IcmpEchoPacket.EchoCode,
            identifier: 1,
            sequenceNumber: 1,
            ReadOnlySpan<byte>.Empty,
            out int typeBytesWritten);
        PacketParseStatus writeCodeStatus = IcmpEchoPacket.TryWrite(
            new byte[IcmpEchoPacket.HeaderLength],
            type: IcmpEchoPacket.TypeEchoRequest,
            code: 1,
            identifier: 1,
            sequenceNumber: 1,
            ReadOnlySpan<byte>.Empty,
            out int codeBytesWritten);

        return HostCheck.Result(parseTypeStatus == PacketParseStatus.UnsupportedIcmpType
            && parseCodeStatus == PacketParseStatus.UnsupportedIcmpCode
            && writeTypeStatus == PacketParseStatus.UnsupportedIcmpType
            && writeCodeStatus == PacketParseStatus.UnsupportedIcmpCode
            && typeBytesWritten == 0
            && codeBytesWritten == 0);
    }

    [HostTest("net.icmp rejects small destination", HostLane.Fast, 5)]
    private static bool TestRejectsSmallDestination()
    {
        byte[] payload = { 0x01 };
        byte[] destination = new byte[IcmpEchoPacket.HeaderLength];

        PacketParseStatus status = IcmpEchoPacket.TryWriteEchoRequest(
            destination,
            identifier: 1,
            sequenceNumber: 1,
            payload,
            out int bytesWritten);

        return HostCheck.Result(status == PacketParseStatus.BufferTooSmall
            && bytesWritten == 0);
    }

    private static byte[] CreateEchoRequest()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] packet = new byte[IcmpEchoPacket.HeaderLength + payload.Length];
        PacketParseStatus status = IcmpEchoPacket.TryWriteEchoRequest(
            packet,
            identifier: 0x1234,
            sequenceNumber: 1,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != packet.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic ICMP echo request.");
        }

        return packet;
    }

    private static void WriteChecksum(byte[] packet)
    {
        packet[2] = 0;
        packet[3] = 0;
        ushort checksum = IcmpEchoPacket.ComputeChecksum(packet);
        packet[2] = (byte)(checksum >> 8);
        packet[3] = (byte)checksum;
    }
}
