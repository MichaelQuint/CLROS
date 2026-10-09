using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpClientHandshakeTests
{
    private const ushort LocalPort = 49152;
    private const ushort RemotePort = 443;
    private const uint ClientInitialSequence = 0x01020304;
    private const uint ServerInitialSequence = 0x8090a0b0;

    [HostTest("net.tcp handshake transitions syn-synack-ack", HostLane.Fast, 0)]
    private static bool TestSynSynAckAckTransition()
    {
        TcpClientHandshake handshake = TcpClientHandshake.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];

        TcpClientHandshakeStatus synStatus = handshake.TryWriteSyn(
            syn,
            windowSize: 0x4000,
            checksum: 0x1111,
            out TcpClientHandshakeTransitionResult synResult);
        if (synStatus != TcpClientHandshakeStatus.Success
            || !synResult.Succeeded
            || synResult.State != TcpClientHandshakeState.SynSent
            || synResult.BytesWritten != TcpHeader.MinHeaderLength
            || handshake.State != TcpClientHandshakeState.SynSent
            || handshake.LocalNextSequenceNumber != ClientInitialSequence + 1)
        {
            return HostCheck.Result(false);
        }

        if (!ParseHeader(syn, out TcpHeader synHeader)
            || synHeader.SourcePort != LocalPort
            || synHeader.DestinationPort != RemotePort
            || synHeader.SequenceNumber != ClientInitialSequence
            || synHeader.AcknowledgementNumber != 0
            || synHeader.Flags != TcpClientHandshake.FlagSyn
            || synHeader.WindowSize != 0x4000
            || synHeader.Checksum != 0x1111)
        {
            return HostCheck.Result(false);
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int synAckBytes = WriteServerSegment(
            synAck,
            ClientInitialSequence + 1,
            TcpClientHandshake.FlagSyn | TcpClientHandshake.FlagAck);

        TcpClientHandshakeStatus receiveStatus = handshake.ReceiveSynAck(
            synAck.Slice(0, synAckBytes),
            out TcpClientHandshakeTransitionResult receiveResult);
        if (receiveStatus != TcpClientHandshakeStatus.Success
            || !receiveResult.Succeeded
            || receiveResult.State != TcpClientHandshakeState.SynSent
            || handshake.State != TcpClientHandshakeState.SynSent
            || handshake.RemoteInitialSequenceNumber != ServerInitialSequence
            || handshake.RemoteNextSequenceNumber != ServerInitialSequence + 1
            || receiveResult.RemoteNextSequenceNumber != ServerInitialSequence + 1)
        {
            return HostCheck.Result(false);
        }

        Span<byte> finalAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpClientHandshakeStatus ackStatus = handshake.TryWriteFinalAck(
            finalAck,
            windowSize: 0x4000,
            checksum: 0,
            out TcpClientHandshakeTransitionResult ackResult);
        if (ackStatus != TcpClientHandshakeStatus.Success
            || !ackResult.Succeeded
            || ackResult.State != TcpClientHandshakeState.Established
            || ackResult.BytesWritten != TcpHeader.MinHeaderLength
            || handshake.State != TcpClientHandshakeState.Established)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(ParseHeader(finalAck, out TcpHeader ackHeader)
            && ackHeader.SourcePort == LocalPort
            && ackHeader.DestinationPort == RemotePort
            && ackHeader.SequenceNumber == ClientInitialSequence + 1
            && ackHeader.AcknowledgementNumber == ServerInitialSequence + 1
            && ackHeader.Flags == TcpClientHandshake.FlagAck
            && ackHeader.WindowSize == 0x4000
            && ackHeader.Checksum == 0);
    }

    [HostTest("net.tcp handshake rejects wrong ack number", HostLane.Fast, 1)]
    private static bool TestRejectsWrongAckNumber()
    {
        TcpClientHandshake handshake = TcpClientHandshake.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        if (handshake.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpClientHandshakeTransitionResult _) != TcpClientHandshakeStatus.Success)
        {
            return HostCheck.Result(false);
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int synAckBytes = WriteServerSegment(
            synAck,
            ClientInitialSequence + 2,
            TcpClientHandshake.FlagSyn | TcpClientHandshake.FlagAck);

        TcpClientHandshakeStatus status = handshake.ReceiveSynAck(
            synAck.Slice(0, synAckBytes),
            out TcpClientHandshakeTransitionResult result);

        return HostCheck.Result(status == TcpClientHandshakeStatus.WrongAcknowledgementNumber
            && !result.Succeeded
            && result.State == TcpClientHandshakeState.Failed
            && handshake.State == TcpClientHandshakeState.Failed);
    }

    [HostTest("net.tcp handshake rejects missing synack flags", HostLane.Fast, 2)]
    private static bool TestRejectsMissingSynAckFlags()
    {
        TcpClientHandshake handshake = TcpClientHandshake.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        if (handshake.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpClientHandshakeTransitionResult _) != TcpClientHandshakeStatus.Success)
        {
            return HostCheck.Result(false);
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int synAckBytes = WriteServerSegment(
            synAck,
            ClientInitialSequence + 1,
            TcpClientHandshake.FlagAck);

        TcpClientHandshakeStatus status = handshake.ReceiveSynAck(
            synAck.Slice(0, synAckBytes),
            out TcpClientHandshakeTransitionResult result);

        return HostCheck.Result(status == TcpClientHandshakeStatus.InvalidFlags
            && !result.Succeeded
            && result.State == TcpClientHandshakeState.Failed
            && handshake.State == TcpClientHandshakeState.Failed);
    }

    [HostTest("net.tcp handshake default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpClientHandshakeTransitionResult defaultResult = default;
        TcpClientHandshakeTransitionResult failedResult =
            TcpClientHandshakeTransitionResult.Fail(
                TcpClientHandshakeStatus.InvalidState,
                TcpClientHandshakeState.Initial,
                PacketParseStatus.Success,
                localNextSequenceNumber: 7,
                remoteNextSequenceNumber: 0);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpClientHandshakeStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == TcpClientHandshakeStatus.InvalidState
            && failedResult.State == TcpClientHandshakeState.Initial
            && failedResult.LocalNextSequenceNumber == 7
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpClientHandshakeTransitionResult>());
    }

    private static int WriteServerSegment(
        Span<byte> segment,
        uint acknowledgementNumber,
        byte flags)
    {
        PacketParseStatus status = TcpHeader.TryWrite(
            segment,
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            acknowledgementNumber,
            flags,
            windowSize: 0x2000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out int bytesWritten);

        return status == PacketParseStatus.Success ? bytesWritten : 0;
    }

    private static bool ParseHeader(ReadOnlySpan<byte> segment, out TcpHeader header)
    {
        return TcpHeader.TryParse(
            segment,
            out header,
            out ReadOnlySpan<byte> options,
            out ReadOnlySpan<byte> payload) == PacketParseStatus.Success
            && options.Length == 0
            && payload.Length == 0;
    }
}
