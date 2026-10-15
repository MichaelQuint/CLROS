using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpPassiveIpv4FramePlannerTests
{
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint ClientInitialSequence = 0x10203040;
    private const uint ServerInitialSequence = 0xa0b0c0d0;
    private const byte PoisonByte = 0xA5;
    private const int TcpChecksumOffset = 16;

    [HostTest("net.tcp.passive.ipv4 writes syn ack frame", HostLane.Fast, 0)]
    private static bool TestWritesSynAckFrame()
    {
        TcpPassiveIpv4FramePlanner planner = CreatePlanner();
        byte[] syn = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        byte[] synAck = new byte[TcpPassiveIpv4FramePlanner.HeaderBytes];

        TcpPassiveIpv4FramePlanStatus status = planner.ReceiveSynFrameAndWriteSynAckFrame(
            syn,
            synAck,
            out TcpPassiveIpv4FramePlanResult result);

        if (status != TcpPassiveIpv4FramePlanStatus.Success
            || !result.Succeeded
            || result.State != TcpPassiveOpenHandshakeState.SynReceived
            || planner.State != TcpPassiveOpenHandshakeState.SynReceived
            || result.BytesWritten != TcpPassiveIpv4FramePlanner.HeaderBytes
            || result.TcpSegmentOffset != EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            || result.TcpSegmentLength != TcpHeader.MinHeaderLength
            || result.LocalMac != LocalMac()
            || result.RemoteMac != RemoteMac()
            || result.LocalAddress != LocalAddress()
            || result.RemoteAddress != RemoteAddress()
            || result.LocalPort != LocalPort
            || result.RemotePort != RemotePort
            || result.LocalNextSequenceNumber != ServerInitialSequence + 1
            || result.RemoteInitialSequenceNumber != ClientInitialSequence
            || result.RemoteNextSequenceNumber != ClientInitialSequence + 1)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(TryParseTcpFrame(
                synAck.AsSpan(0, result.BytesWritten),
                out EthernetHeader ethernet,
                out Ipv4Header ipv4,
                out TcpHeader tcp)
            && ethernet.Source == LocalMac()
            && ethernet.Destination == RemoteMac()
            && ethernet.EtherType == EthernetHeader.EtherTypeIpv4
            && ipv4.Source == LocalAddress()
            && ipv4.Destination == RemoteAddress()
            && ipv4.Protocol == TcpPassiveIpv4FramePlanner.ProtocolTcp
            && ipv4.TimeToLive == 51
            && ipv4.Identification == 0x270
            && tcp.SourcePort == LocalPort
            && tcp.DestinationPort == RemotePort
            && tcp.SequenceNumber == ServerInitialSequence
            && tcp.AcknowledgementNumber == ClientInitialSequence + 1
            && tcp.Flags == (TcpPassiveOpenHandshake.FlagSyn | TcpPassiveOpenHandshake.FlagAck)
            && tcp.WindowSize == 0x3456
            && tcp.Checksum != 0);
    }

    [HostTest("net.tcp.passive.ipv4 writes syn ack checksum", HostLane.Fast, 1)]
    private static bool TestWritesSynAckChecksum()
    {
        TcpPassiveIpv4FramePlanner planner = CreatePlanner();
        byte[] syn = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        byte[] synAck = new byte[TcpPassiveIpv4FramePlanner.HeaderBytes];

        TcpPassiveIpv4FramePlanStatus status = planner.ReceiveSynFrameAndWriteSynAckFrame(
            syn,
            synAck,
            out TcpPassiveIpv4FramePlanResult result);

        if (!TryParseTcpFrame(
                synAck.AsSpan(0, result.BytesWritten),
                out EthernetHeader _,
                out Ipv4Header ipv4,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> tcpSegment))
        {
            return HostCheck.Result(false);
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpPassiveIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);

        return HostCheck.Result(status == TcpPassiveIpv4FramePlanStatus.Success
            && result.Succeeded
            && result.State == TcpPassiveOpenHandshakeState.SynReceived
            && result.TcpHeader.Checksum == tcp.Checksum
            && tcp.Checksum != 0
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == TcpHeader.MinHeaderLength);
    }

    [HostTest("net.tcp.passive.ipv4 accepts final ack frame", HostLane.Fast, 2)]
    private static bool TestAcceptsFinalAckFrame()
    {
        TcpPassiveIpv4FramePlanner planner = PreparedPlanner();
        byte[] finalAck = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence + 1,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagAck);

        TcpPassiveIpv4FramePlanStatus status = planner.ReceiveFinalAckFrame(
            finalAck,
            out TcpPassiveIpv4FramePlanResult result);

        return HostCheck.Result(status == TcpPassiveIpv4FramePlanStatus.Success
            && result.Succeeded
            && result.State == TcpPassiveOpenHandshakeState.Established
            && planner.State == TcpPassiveOpenHandshakeState.Established
            && result.TcpStatus == TcpPassiveOpenHandshakeStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.BytesWritten == 0
            && result.LocalMac == LocalMac()
            && result.RemoteMac == RemoteMac()
            && result.LocalAddress == LocalAddress()
            && result.RemoteAddress == RemoteAddress()
            && result.LocalPort == LocalPort
            && result.RemotePort == RemotePort
            && result.LocalNextSequenceNumber == ServerInitialSequence + 1
            && result.RemoteInitialSequenceNumber == ClientInitialSequence
            && result.RemoteNextSequenceNumber == ClientInitialSequence + 1
            && result.EthernetHeader.Source == RemoteMac()
            && result.EthernetHeader.Destination == LocalMac()
            && result.Ipv4Header.Source == RemoteAddress()
            && result.Ipv4Header.Destination == LocalAddress()
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpHeader.SequenceNumber == ClientInitialSequence + 1
            && result.TcpHeader.AcknowledgementNumber == ServerInitialSequence + 1
            && result.TcpHeader.Flags == TcpPassiveOpenHandshake.FlagAck);
    }

    [HostTest("net.tcp.passive.ipv4 rejects wrong listener identity", HostLane.Fast, 3)]
    private static bool TestRejectsWrongListenerIdentity()
    {
        TcpPassiveIpv4FramePlanner wrongMacPlanner = CreatePlanner();
        byte[] wrongDestinationMac = CreateClientSynFrame(
            RemoteMac(),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x0B, 0xAD),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        TcpPassiveIpv4FramePlanStatus wrongMacStatus = wrongMacPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            wrongDestinationMac,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult wrongMacResult);

        TcpPassiveIpv4FramePlanner wrongIpPlanner = CreatePlanner();
        byte[] wrongDestinationIp = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            new Ipv4Address(192, 0, 2, 200),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        TcpPassiveIpv4FramePlanStatus wrongIpStatus = wrongIpPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            wrongDestinationIp,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult wrongIpResult);

        TcpPassiveIpv4FramePlanner wrongPortPlanner = CreatePlanner();
        byte[] wrongDestinationPort = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort + 1,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        TcpPassiveIpv4FramePlanStatus wrongPortStatus = wrongPortPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            wrongDestinationPort,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult wrongPortResult);

        return HostCheck.Result(wrongMacStatus == TcpPassiveIpv4FramePlanStatus.WrongLocalMac
            && !wrongMacResult.Succeeded
            && wrongMacResult.PacketStatus == PacketParseStatus.Success
            && wrongMacPlanner.State == TcpPassiveOpenHandshakeState.Listen
            && wrongIpStatus == TcpPassiveIpv4FramePlanStatus.WrongLocalAddress
            && !wrongIpResult.Succeeded
            && wrongIpResult.PacketStatus == PacketParseStatus.Success
            && wrongIpResult.Ipv4Header.Destination == new Ipv4Address(192, 0, 2, 200)
            && wrongIpPlanner.State == TcpPassiveOpenHandshakeState.Listen
            && wrongPortStatus == TcpPassiveIpv4FramePlanStatus.WrongTcpPorts
            && !wrongPortResult.Succeeded
            && wrongPortResult.TcpStatus == TcpPassiveOpenHandshakeStatus.UnexpectedPorts
            && wrongPortResult.TcpHeader.DestinationPort == LocalPort + 1
            && wrongPortPlanner.State == TcpPassiveOpenHandshakeState.Failed);
    }

    [HostTest("net.tcp.passive.ipv4 rejects malformed protocol frames", HostLane.Fast, 4)]
    private static bool TestRejectsMalformedProtocolFrames()
    {
        TcpPassiveIpv4FramePlanner shortPlanner = CreatePlanner();
        byte[] shortFrame = new byte[EthernetHeader.HeaderLength - 1];
        TcpPassiveIpv4FramePlanStatus shortStatus = shortPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            shortFrame,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult shortResult);

        TcpPassiveIpv4FramePlanner etherTypePlanner = CreatePlanner();
        byte[] wrongEtherType = new byte[EthernetHeader.HeaderLength];
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(
            wrongEtherType,
            new EthernetHeader(LocalMac(), RemoteMac(), EthernetHeader.EtherTypeArp));
        TcpPassiveIpv4FramePlanStatus etherTypeStatus = etherTypePlanner.ReceiveSynFrameAndWriteSynAckFrame(
            wrongEtherType,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult etherTypeResult);

        TcpPassiveIpv4FramePlanner protocolPlanner = CreatePlanner();
        byte[] wrongProtocol = CreateClientFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            Ipv4Header.ProtocolUdp,
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        TcpPassiveIpv4FramePlanStatus protocolStatus = protocolPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            wrongProtocol,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult protocolResult);

        TcpPassiveIpv4FramePlanner badSynPlanner = CreatePlanner();
        byte[] badSynFlags = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagAck);
        TcpPassiveIpv4FramePlanStatus badSynStatus = badSynPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            badSynFlags,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult badSynResult);

        return HostCheck.Result(shortStatus == TcpPassiveIpv4FramePlanStatus.FrameParseFailed
            && shortResult.PacketStatus == PacketParseStatus.BufferTooSmall
            && shortPlanner.State == TcpPassiveOpenHandshakeState.Listen
            && ethernetStatus == PacketParseStatus.Success
            && etherTypeStatus == TcpPassiveIpv4FramePlanStatus.UnsupportedEtherType
            && etherTypeResult.PacketStatus == PacketParseStatus.UnsupportedEtherType
            && etherTypePlanner.State == TcpPassiveOpenHandshakeState.Listen
            && protocolStatus == TcpPassiveIpv4FramePlanStatus.UnsupportedIpProtocol
            && protocolResult.PacketStatus == PacketParseStatus.UnsupportedIpProtocol
            && protocolPlanner.State == TcpPassiveOpenHandshakeState.Listen
            && badSynStatus == TcpPassiveIpv4FramePlanStatus.InvalidTcpFlags
            && badSynResult.TcpStatus == TcpPassiveOpenHandshakeStatus.InvalidFlags
            && badSynResult.TcpHeader.Flags == TcpPassiveOpenHandshake.FlagAck
            && badSynPlanner.State == TcpPassiveOpenHandshakeState.Failed);
    }

    [HostTest("net.tcp.passive.ipv4 rejects bad final ack", HostLane.Fast, 6)]
    private static bool TestRejectsBadFinalAck()
    {
        TcpPassiveIpv4FramePlanner wrongAckPlanner = PreparedPlanner();
        byte[] wrongAck = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence + 1,
            ServerInitialSequence + 2,
            TcpPassiveOpenHandshake.FlagAck);
        TcpPassiveIpv4FramePlanStatus wrongAckStatus = wrongAckPlanner.ReceiveFinalAckFrame(
            wrongAck,
            out TcpPassiveIpv4FramePlanResult wrongAckResult);

        TcpPassiveIpv4FramePlanner wrongSequencePlanner = PreparedPlanner();
        byte[] wrongSequence = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence + 2,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagAck);
        TcpPassiveIpv4FramePlanStatus wrongSequenceStatus = wrongSequencePlanner.ReceiveFinalAckFrame(
            wrongSequence,
            out TcpPassiveIpv4FramePlanResult wrongSequenceResult);

        TcpPassiveIpv4FramePlanner wrongFlagsPlanner = PreparedPlanner();
        byte[] wrongFlags = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence + 1,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagSyn | TcpPassiveOpenHandshake.FlagAck);
        TcpPassiveIpv4FramePlanStatus wrongFlagsStatus = wrongFlagsPlanner.ReceiveFinalAckFrame(
            wrongFlags,
            out TcpPassiveIpv4FramePlanResult wrongFlagsResult);

        return HostCheck.Result(wrongAckStatus == TcpPassiveIpv4FramePlanStatus.WrongAcknowledgementNumber
            && !wrongAckResult.Succeeded
            && wrongAckResult.TcpStatus == TcpPassiveOpenHandshakeStatus.WrongAcknowledgementNumber
            && wrongAckResult.TcpHeader.AcknowledgementNumber == ServerInitialSequence + 2
            && wrongAckPlanner.State == TcpPassiveOpenHandshakeState.Failed
            && wrongSequenceStatus == TcpPassiveIpv4FramePlanStatus.WrongSequenceNumber
            && !wrongSequenceResult.Succeeded
            && wrongSequenceResult.TcpStatus == TcpPassiveOpenHandshakeStatus.WrongSequenceNumber
            && wrongSequenceResult.TcpHeader.SequenceNumber == ClientInitialSequence + 2
            && wrongSequencePlanner.State == TcpPassiveOpenHandshakeState.Failed
            && wrongFlagsStatus == TcpPassiveIpv4FramePlanStatus.InvalidTcpFlags
            && !wrongFlagsResult.Succeeded
            && wrongFlagsResult.TcpStatus == TcpPassiveOpenHandshakeStatus.InvalidFlags
            && wrongFlagsResult.TcpHeader.Flags == (TcpPassiveOpenHandshake.FlagSyn | TcpPassiveOpenHandshake.FlagAck)
            && wrongFlagsPlanner.State == TcpPassiveOpenHandshakeState.Failed);
    }

    [HostTest("net.tcp.passive.ipv4 rejects bad tcp checksum", HostLane.Fast, 5)]
    private static bool TestRejectsBadTcpChecksum()
    {
        TcpPassiveIpv4FramePlanner synPlanner = CreatePlanner();
        byte[] badSyn = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        badSyn[EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpChecksumOffset] ^= 0x01;

        TcpPassiveIpv4FramePlanStatus synStatus = synPlanner.ReceiveSynFrameAndWriteSynAckFrame(
            badSyn,
            new byte[TcpPassiveIpv4FramePlanner.HeaderBytes],
            out TcpPassiveIpv4FramePlanResult synResult);

        TcpPassiveIpv4FramePlanner finalAckPlanner = PreparedPlanner();
        byte[] badFinalAck = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence + 1,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagAck);
        badFinalAck[EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpChecksumOffset] ^= 0x01;

        TcpPassiveIpv4FramePlanStatus finalAckStatus = finalAckPlanner.ReceiveFinalAckFrame(
            badFinalAck,
            out TcpPassiveIpv4FramePlanResult finalAckResult);

        return HostCheck.Result(synStatus == TcpPassiveIpv4FramePlanStatus.BadTcpChecksum
            && !synResult.Succeeded
            && synResult.TcpStatus == TcpPassiveOpenHandshakeStatus.Success
            && synResult.PacketStatus == PacketParseStatus.BadChecksum
            && synResult.State == TcpPassiveOpenHandshakeState.Listen
            && synResult.TcpHeader.SourcePort == RemotePort
            && synResult.TcpHeader.DestinationPort == LocalPort
            && synPlanner.State == TcpPassiveOpenHandshakeState.Listen
            && finalAckStatus == TcpPassiveIpv4FramePlanStatus.BadTcpChecksum
            && !finalAckResult.Succeeded
            && finalAckResult.TcpStatus == TcpPassiveOpenHandshakeStatus.Success
            && finalAckResult.PacketStatus == PacketParseStatus.BadChecksum
            && finalAckResult.State == TcpPassiveOpenHandshakeState.SynReceived
            && finalAckResult.TcpHeader.SequenceNumber == ClientInitialSequence + 1
            && finalAckResult.TcpHeader.AcknowledgementNumber == ServerInitialSequence + 1
            && finalAckPlanner.State == TcpPassiveOpenHandshakeState.SynReceived);
    }

    [HostTest("net.tcp.passive.ipv4 rejects short output", HostLane.Fast, 7)]
    private static bool TestRejectsShortOutput()
    {
        TcpPassiveIpv4FramePlanner planner = CreatePlanner();
        byte[] syn = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        byte[] shortSynAck = PoisonedFrame(TcpPassiveIpv4FramePlanner.HeaderBytes);

        TcpPassiveIpv4FramePlanStatus status = planner.ReceiveSynFrameAndWriteSynAckFrame(
            syn,
            shortSynAck.AsSpan(0, shortSynAck.Length - 1),
            out TcpPassiveIpv4FramePlanResult result);

        return HostCheck.Result(status == TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall
            && !result.Succeeded
            && result.TcpStatus == TcpPassiveOpenHandshakeStatus.BufferTooSmall
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && planner.State == TcpPassiveOpenHandshakeState.Listen
            && shortSynAck[0] == PoisonByte
            && shortSynAck[^1] == PoisonByte);
    }

    [HostTest("net.tcp.passive.ipv4 default result is not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpPassiveIpv4FramePlannerInfo info = new(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            LocalPort,
            RemotePort,
            ServerInitialSequence,
            windowSize: 0x3456,
            timeToLive: 51,
            identification: 0x270,
            tcpChecksum: 0x3333);
        TcpPassiveIpv4FramePlanResult defaultResult = default;
        TcpPassiveIpv4FramePlanResult success = TcpPassiveIpv4FramePlanResult.Success(
            TcpPassiveOpenHandshakeState.SynReceived,
            TcpPassiveOpenHandshakeStatus.Success,
            PacketParseStatus.Success,
            default,
            default,
            default,
            TcpPassiveIpv4FramePlanner.HeaderBytes,
            EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
            TcpHeader.MinHeaderLength,
            info,
            ServerInitialSequence + 1,
            ClientInitialSequence,
            ClientInitialSequence + 1);
        TcpPassiveIpv4FramePlanResult failure = TcpPassiveIpv4FramePlanResult.Fail(
            TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall,
            TcpPassiveOpenHandshakeState.Listen,
            TcpPassiveOpenHandshakeStatus.BufferTooSmall,
            PacketParseStatus.BufferTooSmall,
            default,
            default,
            default,
            tcpSegmentOffset: 0,
            tcpSegmentLength: 0,
            info,
            ServerInitialSequence,
            remoteInitialSequenceNumber: 0,
            remoteNextSequenceNumber: 0);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpPassiveIpv4FramePlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.State == TcpPassiveOpenHandshakeState.SynReceived
            && success.TcpStatus == TcpPassiveOpenHandshakeStatus.Success
            && success.PacketStatus == PacketParseStatus.Success
            && success.BytesWritten == TcpPassiveIpv4FramePlanner.HeaderBytes
            && success.TcpSegmentOffset == EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            && success.TcpSegmentLength == TcpHeader.MinHeaderLength
            && success.LocalMac == LocalMac()
            && success.RemoteMac == RemoteMac()
            && success.LocalAddress == LocalAddress()
            && success.RemoteAddress == RemoteAddress()
            && success.LocalPort == LocalPort
            && success.RemotePort == RemotePort
            && success.TimeToLive == 51
            && success.Identification == 0x270
            && success.LocalNextSequenceNumber == ServerInitialSequence + 1
            && success.RemoteInitialSequenceNumber == ClientInitialSequence
            && success.RemoteNextSequenceNumber == ClientInitialSequence + 1
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall
            && failure.State == TcpPassiveOpenHandshakeState.Listen
            && failure.TcpStatus == TcpPassiveOpenHandshakeStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.BytesWritten == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpPassiveIpv4FramePlanResult>());
    }

    [HostTest("net.tcp.passive.ipv4 pending reply writer preserves state and short output", HostLane.Fast, 9)]
    private static void PendingReplyWriter()
    {
        var planner = CreatePlanner(); byte[] output = PoisonedFrame(TcpPassiveIpv4FramePlanner.HeaderBytes);
        byte[] original = output.ToArray();
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.InvalidState, planner.WritePendingSynAckFrame(output, out _));
        HostCheck.True(output.AsSpan().SequenceEqual(original));
        planner = PreparedPlanner();
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall,
            planner.WritePendingSynAckFrame(output.AsSpan(0, output.Length - 1), out _));
        HostCheck.True(output.AsSpan().SequenceEqual(original));
        HostCheck.Equal(TcpPassiveOpenHandshakeState.SynReceived, planner.State);
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.Success, planner.WritePendingSynAckFrame(output, out var reply));
        HostCheck.True(reply.Succeeded); HostCheck.Equal(ServerInitialSequence + 1, planner.LocalNextSequenceNumber);
        HostCheck.Equal(ClientInitialSequence + 1, planner.RemoteNextSequenceNumber);
        byte[] ack = CreateClientSynFrame(RemoteMac(), LocalMac(), RemoteAddress(), LocalAddress(),
            RemotePort, LocalPort, ClientInitialSequence + 1, ServerInitialSequence + 1, TcpPassiveOpenHandshake.FlagAck);
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.Success, planner.ReceiveFinalAckFrame(ack, out _));
        original = output.ToArray();
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.InvalidState, planner.WritePendingSynAckFrame(output, out _));
        HostCheck.Equal(TcpPassiveOpenHandshakeState.Established, planner.State);
        HostCheck.True(output.AsSpan().SequenceEqual(original));
    }

    private static TcpPassiveIpv4FramePlanner PreparedPlanner()
    {
        TcpPassiveIpv4FramePlanner planner = CreatePlanner();
        byte[] syn = CreateClientSynFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn);
        byte[] synAck = new byte[TcpPassiveIpv4FramePlanner.HeaderBytes];
        if (planner.ReceiveSynFrameAndWriteSynAckFrame(
                syn,
                synAck,
                out TcpPassiveIpv4FramePlanResult result) != TcpPassiveIpv4FramePlanStatus.Success
            || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to prepare TCP passive IPv4 frame planner.");
        }

        return planner;
    }

    private static TcpPassiveIpv4FramePlanner CreatePlanner()
    {
        return TcpPassiveIpv4FramePlanner.Create(
            new TcpPassiveIpv4FramePlannerInfo(
                LocalMac(),
                RemoteMac(),
                LocalAddress(),
                RemoteAddress(),
                LocalPort,
                RemotePort,
                ServerInitialSequence,
                windowSize: 0x3456,
                timeToLive: 51,
                identification: 0x270,
                tcpChecksum: 0x3333));
    }

    private static byte[] CreateClientSynFrame(
        MacAddress sourceMac,
        MacAddress destinationMac,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags)
    {
        return CreateClientFrame(
            sourceMac,
            destinationMac,
            sourceAddress,
            destinationAddress,
            TcpPassiveIpv4FramePlanner.ProtocolTcp,
            sourcePort,
            destinationPort,
            sequenceNumber,
            acknowledgementNumber,
            flags);
    }

    private static byte[] CreateClientFrame(
        MacAddress sourceMac,
        MacAddress destinationMac,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress,
        byte protocol,
        ushort sourcePort,
        ushort destinationPort,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags)
    {
        byte[] frame = new byte[TcpPassiveIpv4FramePlanner.HeaderBytes];
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength];
        PacketParseStatus tcpStatus = TcpHeader.TryWrite(
            segment,
            sourcePort,
            destinationPort,
            sequenceNumber,
            acknowledgementNumber,
            flags,
            windowSize: 0x2000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out int tcpBytesWritten);
        if (tcpStatus != PacketParseStatus.Success || tcpBytesWritten != TcpHeader.MinHeaderLength)
        {
            throw new InvalidOperationException("Failed to build TCP segment fixture.");
        }

        if (protocol == TcpPassiveIpv4FramePlanner.ProtocolTcp)
        {
            Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
                sourceAddress,
                destinationAddress,
                TcpPassiveIpv4FramePlanner.ProtocolTcp,
                segment.Slice(0, tcpBytesWritten),
                out Ipv4TransportChecksumResult checksumResult);
            if (checksumStatus != Ipv4TransportChecksumStatus.Success)
            {
                throw new InvalidOperationException("Failed to compute TCP checksum fixture.");
            }

            WriteTcpChecksum(segment, checksumResult.Checksum);
        }

        EthernetHeader ethernet = new EthernetHeader(
            destinationMac,
            sourceMac,
            EthernetHeader.EtherTypeIpv4);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build Ethernet fixture.");
        }

        Ipv4Header ipv4 = new Ipv4Header(
            sourceAddress,
            destinationAddress,
            protocol,
            (ushort)(Ipv4Header.MinHeaderLength + tcpBytesWritten),
            timeToLive: 64,
            identification: 0x672,
            flagsAndFragmentOffset: 0,
            typeOfService: 0,
            headerChecksum: 0,
            headerLength: Ipv4Header.MinHeaderLength);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            frame.AsSpan(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
            ipv4,
            out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success || ipv4BytesWritten != Ipv4Header.MinHeaderLength)
        {
            throw new InvalidOperationException("Failed to build IPv4 fixture.");
        }

        segment.Slice(0, tcpBytesWritten).CopyTo(
            frame.AsSpan(EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength));
        return frame;
    }

    private static void WriteTcpChecksum(Span<byte> segment, ushort checksum)
    {
        segment[TcpChecksumOffset] = (byte)(checksum >> 8);
        segment[TcpChecksumOffset + 1] = (byte)checksum;
    }

    private static bool TryParseTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernet,
        out Ipv4Header ipv4,
        out TcpHeader tcp)
    {
        return TryParseTcpFrame(
            frame,
            out ethernet,
            out ipv4,
            out tcp,
            out ReadOnlySpan<byte> _);
    }

    private static bool TryParseTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernet,
        out Ipv4Header ipv4,
        out TcpHeader tcp,
        out ReadOnlySpan<byte> tcpSegment)
    {
        tcp = default;
        ipv4 = default;
        tcpSegment = default;

        if (EthernetHeader.TryParse(
                frame,
                out ethernet,
                out ReadOnlySpan<byte> ethernetPayload) != PacketParseStatus.Success)
        {
            return false;
        }

        if (Ipv4Header.TryParse(
                ethernetPayload,
                out ipv4,
                out tcpSegment) != PacketParseStatus.Success)
        {
            return false;
        }

        return TcpHeader.TryParse(
                tcpSegment,
                out tcp,
                out ReadOnlySpan<byte> options,
                out ReadOnlySpan<byte> payload) == PacketParseStatus.Success
            && options.Length == 0
            && payload.Length == 0;
    }

    private static byte[] PoisonedFrame(int length)
    {
        byte[] frame = new byte[length];
        Array.Fill(frame, PoisonByte);
        return frame;
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x27, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x27, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 27);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 27);
    }
}
