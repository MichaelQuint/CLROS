using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpConnectionCloseTests
{
    private const ushort LocalPort = 49180;
    private const ushort RemotePort = 8443;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [HostTest("net.tcp close active fin ack", HostLane.Fast, 0)]
    private static bool TestActiveFinAckCloseFlow()
    {
        TcpConnectionClose close = TcpConnectionClose.CreateEstablished(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence);

        Span<byte> localFinAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus finStatus = close.TryWriteActiveFinAck(
            localFinAck,
            windowSize: 0x4000,
            checksum: 0x1111,
            out TcpConnectionCloseTransitionResult finResult);
        if (finStatus != TcpConnectionCloseStatus.Success
            || !finResult.Succeeded
            || finResult.State != TcpConnectionCloseState.FinWait1
            || finResult.BytesWritten != TcpHeader.MinHeaderLength
            || finResult.LocalNextSequenceNumber != LocalNextSequence + 1
            || close.State != TcpConnectionCloseState.FinWait1
            || !ParseHeader(localFinAck, out TcpHeader localFinAckHeader)
            || localFinAckHeader.SourcePort != LocalPort
            || localFinAckHeader.DestinationPort != RemotePort
            || localFinAckHeader.SequenceNumber != LocalNextSequence
            || localFinAckHeader.AcknowledgementNumber != RemoteNextSequence
            || localFinAckHeader.Flags != (TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck)
            || localFinAckHeader.WindowSize != 0x4000
            || localFinAckHeader.Checksum != 0x1111)
        {
            return HostCheck.Result(false);
        }

        Span<byte> remoteAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int remoteAckBytes = WriteRemoteSegment(
            remoteAck,
            RemoteNextSequence,
            LocalNextSequence + 1,
            TcpConnectionClose.FlagAck);
        TcpConnectionCloseStatus ackStatus = close.ReceiveActiveFinAcknowledgement(
            remoteAck.Slice(0, remoteAckBytes),
            out TcpConnectionCloseTransitionResult ackResult);
        if (ackStatus != TcpConnectionCloseStatus.Success
            || !ackResult.Succeeded
            || ackResult.State != TcpConnectionCloseState.FinWait2
            || close.State != TcpConnectionCloseState.FinWait2
            || close.LocalNextSequenceNumber != LocalNextSequence + 1
            || close.RemoteNextSequenceNumber != RemoteNextSequence)
        {
            return HostCheck.Result(false);
        }

        Span<byte> remoteFinAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int remoteFinAckBytes = WriteRemoteSegment(
            remoteFinAck,
            RemoteNextSequence,
            LocalNextSequence + 1,
            TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck);
        Span<byte> finalAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus remoteFinStatus = close.ReceiveActiveFinAckAndWriteAck(
            remoteFinAck.Slice(0, remoteFinAckBytes),
            finalAck,
            windowSize: 0x4000,
            checksum: 0,
            out TcpConnectionCloseTransitionResult remoteFinResult);

        return HostCheck.Result(remoteFinStatus == TcpConnectionCloseStatus.Success
            && remoteFinResult.Succeeded
            && remoteFinResult.State == TcpConnectionCloseState.TimeWait
            && remoteFinResult.BytesWritten == TcpHeader.MinHeaderLength
            && close.State == TcpConnectionCloseState.TimeWait
            && close.LocalNextSequenceNumber == LocalNextSequence + 1
            && close.RemoteNextSequenceNumber == RemoteNextSequence + 1
            && ParseHeader(finalAck, out TcpHeader finalAckHeader)
            && finalAckHeader.SourcePort == LocalPort
            && finalAckHeader.DestinationPort == RemotePort
            && finalAckHeader.SequenceNumber == LocalNextSequence + 1
            && finalAckHeader.AcknowledgementNumber == RemoteNextSequence + 1
            && finalAckHeader.Flags == TcpConnectionClose.FlagAck);
    }

    [HostTest("net.tcp close passive fin ack", HostLane.Fast, 1)]
    private static bool TestPassiveFinAckCloseFlow()
    {
        TcpConnectionClose close = TcpConnectionClose.CreateEstablished(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence);

        Span<byte> remoteFinAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int remoteFinAckBytes = WriteRemoteSegment(
            remoteFinAck,
            RemoteNextSequence,
            LocalNextSequence,
            TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck);
        Span<byte> localAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus receiveFinStatus = close.ReceivePassiveFinAckAndWriteAck(
            remoteFinAck.Slice(0, remoteFinAckBytes),
            localAck,
            windowSize: 0x4000,
            checksum: 0x2222,
            out TcpConnectionCloseTransitionResult receiveFinResult);
        if (receiveFinStatus != TcpConnectionCloseStatus.Success
            || !receiveFinResult.Succeeded
            || receiveFinResult.State != TcpConnectionCloseState.CloseWait
            || close.State != TcpConnectionCloseState.CloseWait
            || close.LocalNextSequenceNumber != LocalNextSequence
            || close.RemoteNextSequenceNumber != RemoteNextSequence + 1
            || !ParseHeader(localAck, out TcpHeader localAckHeader)
            || localAckHeader.SourcePort != LocalPort
            || localAckHeader.DestinationPort != RemotePort
            || localAckHeader.SequenceNumber != LocalNextSequence
            || localAckHeader.AcknowledgementNumber != RemoteNextSequence + 1
            || localAckHeader.Flags != TcpConnectionClose.FlagAck
            || localAckHeader.Checksum != 0x2222)
        {
            return HostCheck.Result(false);
        }

        Span<byte> localFinAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus localFinStatus = close.TryWritePassiveFinAck(
            localFinAck,
            windowSize: 0x4000,
            checksum: 0,
            out TcpConnectionCloseTransitionResult localFinResult);
        if (localFinStatus != TcpConnectionCloseStatus.Success
            || !localFinResult.Succeeded
            || localFinResult.State != TcpConnectionCloseState.LastAck
            || close.State != TcpConnectionCloseState.LastAck
            || close.LocalNextSequenceNumber != LocalNextSequence + 1
            || !ParseHeader(localFinAck, out TcpHeader localFinAckHeader)
            || localFinAckHeader.SequenceNumber != LocalNextSequence
            || localFinAckHeader.AcknowledgementNumber != RemoteNextSequence + 1
            || localFinAckHeader.Flags != (TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck))
        {
            return HostCheck.Result(false);
        }

        Span<byte> remoteAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int remoteAckBytes = WriteRemoteSegment(
            remoteAck,
            RemoteNextSequence + 1,
            LocalNextSequence + 1,
            TcpConnectionClose.FlagAck);
        TcpConnectionCloseStatus closeStatus = close.ReceivePassiveFinAcknowledgement(
            remoteAck.Slice(0, remoteAckBytes),
            out TcpConnectionCloseTransitionResult closeResult);

        return HostCheck.Result(closeStatus == TcpConnectionCloseStatus.Success
            && closeResult.Succeeded
            && closeResult.State == TcpConnectionCloseState.Closed
            && close.State == TcpConnectionCloseState.Closed
            && close.LocalNextSequenceNumber == LocalNextSequence + 1
            && close.RemoteNextSequenceNumber == RemoteNextSequence + 1);
    }

    [HostTest("net.tcp close handles reset", HostLane.Fast, 2)]
    private static bool TestHandlesReset()
    {
        TcpConnectionClose close = TcpConnectionClose.CreateEstablished(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence);

        Span<byte> reset = stackalloc byte[TcpHeader.MinHeaderLength];
        int resetBytes = WriteRemoteSegment(
            reset,
            RemoteNextSequence,
            LocalNextSequence,
            TcpConnectionClose.FlagRst | TcpConnectionClose.FlagAck);

        TcpConnectionCloseStatus resetStatus = close.ReceiveReset(
            reset.Slice(0, resetBytes),
            out TcpConnectionCloseTransitionResult resetResult);
        if (resetStatus != TcpConnectionCloseStatus.Success
            || !resetResult.Succeeded
            || resetResult.State != TcpConnectionCloseState.Reset
            || close.State != TcpConnectionCloseState.Reset
            || close.LocalNextSequenceNumber != LocalNextSequence
            || close.RemoteNextSequenceNumber != RemoteNextSequence)
        {
            return HostCheck.Result(false);
        }

        Span<byte> localFin = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus afterResetStatus = close.TryWriteActiveFinAck(
            localFin,
            windowSize: 0x4000,
            checksum: 0,
            out TcpConnectionCloseTransitionResult afterResetResult);

        return HostCheck.Result(afterResetStatus == TcpConnectionCloseStatus.InvalidState
            && !afterResetResult.Succeeded
            && afterResetResult.State == TcpConnectionCloseState.Reset
            && close.State == TcpConnectionCloseState.Reset);
    }

    [HostTest("net.tcp close rejects invalid transition", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidTransition()
    {
        TcpConnectionClose close = TcpConnectionClose.CreateEstablished(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence);

        Span<byte> remoteAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int remoteAckBytes = WriteRemoteSegment(
            remoteAck,
            RemoteNextSequence,
            LocalNextSequence,
            TcpConnectionClose.FlagAck);
        TcpConnectionCloseStatus earlyAckStatus = close.ReceiveActiveFinAcknowledgement(
            remoteAck.Slice(0, remoteAckBytes),
            out TcpConnectionCloseTransitionResult earlyAckResult);
        if (earlyAckStatus != TcpConnectionCloseStatus.InvalidState
            || earlyAckResult.Succeeded
            || earlyAckResult.State != TcpConnectionCloseState.Established
            || close.State != TcpConnectionCloseState.Established)
        {
            return HostCheck.Result(false);
        }

        Span<byte> passiveFin = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus passiveFinStatus = close.TryWritePassiveFinAck(
            passiveFin,
            windowSize: 0x4000,
            checksum: 0,
            out TcpConnectionCloseTransitionResult passiveFinResult);
        if (passiveFinStatus != TcpConnectionCloseStatus.InvalidState
            || passiveFinResult.Succeeded
            || passiveFinResult.State != TcpConnectionCloseState.Established
            || close.State != TcpConnectionCloseState.Established)
        {
            return HostCheck.Result(false);
        }

        Span<byte> activeFin = stackalloc byte[TcpHeader.MinHeaderLength];
        if (close.TryWriteActiveFinAck(
                activeFin,
                windowSize: 0x4000,
                checksum: 0,
                out TcpConnectionCloseTransitionResult _) != TcpConnectionCloseStatus.Success)
        {
            return HostCheck.Result(false);
        }

        Span<byte> secondFin = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus secondFinStatus = close.TryWriteActiveFinAck(
            secondFin,
            windowSize: 0x4000,
            checksum: 0,
            out TcpConnectionCloseTransitionResult secondFinResult);

        return HostCheck.Result(secondFinStatus == TcpConnectionCloseStatus.InvalidState
            && !secondFinResult.Succeeded
            && secondFinResult.State == TcpConnectionCloseState.FinWait1
            && close.State == TcpConnectionCloseState.FinWait1
            && close.LocalNextSequenceNumber == LocalNextSequence + 1
            && close.RemoteNextSequenceNumber == RemoteNextSequence);
    }

    [HostTest("net.tcp close default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpConnectionCloseTransitionResult defaultResult = default;
        TcpConnectionCloseTransitionResult failedResult =
            TcpConnectionCloseTransitionResult.Fail(
                TcpConnectionCloseStatus.InvalidState,
                TcpConnectionCloseState.Established,
                PacketParseStatus.Success,
                LocalNextSequence,
                RemoteNextSequence);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpConnectionCloseStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == TcpConnectionCloseStatus.InvalidState
            && failedResult.State == TcpConnectionCloseState.Established
            && failedResult.LocalNextSequenceNumber == LocalNextSequence
            && failedResult.RemoteNextSequenceNumber == RemoteNextSequence
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpConnectionCloseTransitionResult>());
    }

    private static int WriteRemoteSegment(
        Span<byte> segment,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags)
    {
        PacketParseStatus status = TcpHeader.TryWrite(
            segment,
            RemotePort,
            LocalPort,
            sequenceNumber,
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
