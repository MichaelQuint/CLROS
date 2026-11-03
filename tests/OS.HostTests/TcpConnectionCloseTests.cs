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

    [HostTest("net.tcp close combined FIN ACK enters TimeWait directly", HostLane.Fast, 5)]
    private static void CombinedFinAck()
    {
        var close = TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence);
        Span<byte> output = stackalloc byte[20], incoming = stackalloc byte[20];
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.TryWriteActiveFinAck(output, 32, 0, out _));
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence + 1, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveActiveFinAckAndWriteAck(incoming, output, 24, 0x1234, out var result));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, close.State);
        HostCheck.Equal(LocalNextSequence + 1, close.LocalNextSequenceNumber);
        HostCheck.Equal(RemoteNextSequence + 1, close.RemoteNextSequenceNumber);
        HostCheck.Equal(20, result.BytesWritten);
        HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryParse(output, out var ack, out _, out var payload));
        HostCheck.Equal((byte)0x10, ack.Flags);
        HostCheck.Equal((ushort)24, ack.WindowSize);
        HostCheck.Equal((ushort)0x1234, ack.Checksum);
        HostCheck.Equal(RemoteNextSequence + 1, ack.AcknowledgementNumber);
        HostCheck.Equal(0, payload.Length);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.CompleteTimeWait(out result));
        HostCheck.Equal(TcpConnectionCloseState.Closed, result.State);
        HostCheck.Equal(0, result.BytesWritten);
        HostCheck.Equal(TcpConnectionCloseStatus.InvalidState, close.CompleteTimeWait(out _));
    }

    [HostTest("net.tcp close simultaneous FIN waits for ACK without consuming FIN twice", HostLane.Fast, 6)]
    private static void SimultaneousFin()
    {
        var close = TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence);
        Span<byte> output = stackalloc byte[20], incoming = stackalloc byte[20];
        close.TryWriteActiveFinAck(output, 32, 0, out _);
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveActiveFinAckAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.Closing, close.State);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveRepeatedFinAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.Closing, close.State);
        HostCheck.Equal(RemoteNextSequence + 1, close.RemoteNextSequenceNumber);
        WriteRemoteSegment(incoming, RemoteNextSequence + 1, LocalNextSequence, 0x10);
        HostCheck.Equal(TcpConnectionCloseStatus.WrongAcknowledgementNumber, close.ReceiveActiveFinAcknowledgement(incoming, out _));
        HostCheck.Equal(TcpConnectionCloseState.Closing, close.State);
        WriteRemoteSegment(incoming, RemoteNextSequence + 1, LocalNextSequence + 1, 0x10);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveActiveFinAcknowledgement(incoming, out _));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, close.State);
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveRepeatedFinAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, close.State);
    }

    [HostTest("net.tcp close repeated FIN may acknowledge the outstanding FIN", HostLane.Fast, 7)]
    private static void RepeatedAcknowledgedFin()
    {
        Span<byte> output = stackalloc byte[20], incoming = stackalloc byte[20];
        foreach (bool passive in new[] { false, true })
        {
            var close = TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence);
            WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence, 0x11);
            if (passive)
            {
                HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceivePassiveFinAckAndWriteAck(incoming, output, 32, 0, out _));
                HostCheck.Equal(TcpConnectionCloseState.CloseWait, close.State);
                HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveRepeatedFinAndWriteAck(incoming, output, 32, 0, out _));
                HostCheck.Equal(TcpConnectionCloseState.CloseWait, close.State);
                HostCheck.Equal(TcpConnectionCloseStatus.Success, close.TryWritePassiveFinAck(output, 32, 0, out _));
            }
            else
            {
                close.TryWriteActiveFinAck(output, 32, 0, out _);
                close.ReceiveActiveFinAckAndWriteAck(incoming, output, 32, 0, out _);
            }
            WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence + 1, 0x11);
            HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveRepeatedFinAndWriteAck(incoming, output, 32, 0, out var result));
            HostCheck.Equal(passive ? TcpConnectionCloseState.Closed : TcpConnectionCloseState.TimeWait, result.State);
            HostCheck.Equal(LocalNextSequence + 1, close.LocalNextSequenceNumber);
            HostCheck.Equal(RemoteNextSequence + 1, close.RemoteNextSequenceNumber);
        }
    }

    [HostTest("net.tcp close combined and repeated FIN failures preserve transactional state", HostLane.Fast, 8)]
    private static void FinFailureControls()
    {
        var close = TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence);
        Span<byte> output = stackalloc byte[20], incoming = stackalloc byte[24];
        HostCheck.Equal(TcpConnectionCloseStatus.InvalidState, close.CompleteTimeWait(out _));
        close.TryWriteActiveFinAck(output, 32, 0, out _);
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence + 1, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.BufferTooSmall, close.ReceiveActiveFinAckAndWriteAck(incoming.Slice(0, 20), output.Slice(0, 19), 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.FinWait1, close.State);
        HostCheck.Equal(RemoteNextSequence, close.RemoteNextSequenceNumber);
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence + 2, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.WrongAcknowledgementNumber, close.ReceiveActiveFinAckAndWriteAck(incoming.Slice(0, 20), output, 32, 0, out _));
        WriteRemoteSegment(incoming, RemoteNextSequence + 1, LocalNextSequence + 1, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.WrongSequenceNumber, close.ReceiveActiveFinAckAndWriteAck(incoming.Slice(0, 20), output, 32, 0, out _));
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence + 1, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.UnexpectedPayload, close.ReceiveActiveFinAckAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.FinWait1, close.State);
        close.ReceiveActiveFinAckAndWriteAck(incoming.Slice(0, 20), output, 32, 0, out _);
        HostCheck.Equal(TcpConnectionCloseStatus.BufferTooSmall, close.ReceiveRepeatedFinAndWriteAck(incoming.Slice(0, 20), output.Slice(0, 19), 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, close.State);
        HostCheck.Equal(RemoteNextSequence + 1, close.RemoteNextSequenceNumber);
        incoming[13] = 0x10;
        HostCheck.Equal(TcpConnectionCloseStatus.InvalidFlags, close.ReceiveRepeatedFinAndWriteAck(incoming.Slice(0, 20), output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, close.State);
    }

    [HostTest("net.tcp close simultaneous FIN and duplicate controls preserve sequence wrap", HostLane.Fast, 9)]
    private static void WrappedFin()
    {
        var close = TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, uint.MaxValue, uint.MaxValue);
        Span<byte> output = stackalloc byte[20], incoming = stackalloc byte[20];
        close.TryWriteActiveFinAck(output, 32, 0, out _);
        WriteRemoteSegment(incoming, uint.MaxValue, uint.MaxValue, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveActiveFinAckAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.Closing, close.State);
        HostCheck.Equal(0U, close.LocalNextSequenceNumber);
        HostCheck.Equal(0U, close.RemoteNextSequenceNumber);
        WriteRemoteSegment(incoming, uint.MaxValue, 0, 0x11);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.ReceiveRepeatedFinAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, close.State);
        HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryParse(output, out var ack, out _, out _));
        HostCheck.Equal(0U, ack.SequenceNumber);
        HostCheck.Equal(0U, ack.AcknowledgementNumber);
    }

    [HostTest("net.tcp close rejects reset and stale controls after TimeWait completion", HostLane.Fast, 10)]
    private static void CompletedState()
    {
        var close = TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence);
        Span<byte> output = stackalloc byte[20], incoming = stackalloc byte[20];
        close.TryWriteActiveFinAck(output, 32, 0, out _);
        WriteRemoteSegment(incoming, RemoteNextSequence, LocalNextSequence + 1, 0x11);
        close.ReceiveActiveFinAckAndWriteAck(incoming, output, 32, 0, out _);
        HostCheck.Equal(TcpConnectionCloseStatus.Success, close.CompleteTimeWait(out _));
        HostCheck.Equal(TcpConnectionCloseStatus.InvalidState, close.ReceiveRepeatedFinAndWriteAck(incoming, output, 32, 0, out _));
        HostCheck.Equal(TcpConnectionCloseStatus.InvalidState, close.ReceiveActiveFinAckAndWriteAck(incoming, output, 32, 0, out _));
        WriteRemoteSegment(incoming, RemoteNextSequence + 1, LocalNextSequence + 1, 0x14);
        HostCheck.Equal(TcpConnectionCloseStatus.InvalidState, close.ReceiveReset(incoming, out _));
        HostCheck.Equal(TcpConnectionCloseState.Closed, close.State);
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
