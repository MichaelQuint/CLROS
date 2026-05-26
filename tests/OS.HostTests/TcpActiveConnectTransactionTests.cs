using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpActiveConnectTransactionTests
{
    private const ushort LocalPort = 49160;
    private const ushort RemotePort = 443;
    private const uint ClientInitialSequence = 0x11223344;
    private const uint ServerInitialSequence = 0x55667788;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp active connect establishes", TestActiveConnectEstablishes, ref failures);
        Run("net.tcp active connect rejects bad synack", TestActiveConnectRejectsBadSynAck, ref failures);
        Run("net.tcp active connect times out", TestActiveConnectTimesOut, ref failures);
        Run("net.tcp active connect default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP active-connect transaction host tests failed: " + failures);
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

    private static bool TestActiveConnectEstablishes()
    {
        TcpActiveConnectTransaction transaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence,
            ServerInitialSequence);

        if (!transaction.RequiresRemoteInitialSequenceNumber
            || transaction.ExpectedRemoteInitialSequenceNumber != ServerInitialSequence
            || transaction.State != TcpActiveConnectState.Closed)
        {
            return false;
        }

        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpActiveConnectStatus synStatus = transaction.TryWriteSyn(
            syn,
            windowSize: 0x4000,
            checksum: 0x1111,
            out TcpActiveConnectTransitionResult synResult);
        if (synStatus != TcpActiveConnectStatus.Success
            || !synResult.Succeeded
            || synResult.State != TcpActiveConnectState.SynSent
            || synResult.BytesWritten != TcpHeader.MinHeaderLength
            || transaction.State != TcpActiveConnectState.SynSent
            || transaction.LocalNextSequenceNumber != ClientInitialSequence + 1)
        {
            return false;
        }

        if (!ParseHeader(syn, out TcpHeader synHeader)
            || synHeader.SourcePort != LocalPort
            || synHeader.DestinationPort != RemotePort
            || synHeader.SequenceNumber != ClientInitialSequence
            || synHeader.AcknowledgementNumber != 0
            || synHeader.Flags != TcpActiveConnectTransaction.FlagSyn
            || synHeader.WindowSize != 0x4000
            || synHeader.Checksum != 0x1111)
        {
            return false;
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int synAckBytes = WriteServerSegment(
            synAck,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpActiveConnectStatus receiveStatus = transaction.ReceiveSynAck(
            synAck.Slice(0, synAckBytes),
            out TcpActiveConnectTransitionResult receiveResult);
        if (receiveStatus != TcpActiveConnectStatus.Success
            || !receiveResult.Succeeded
            || receiveResult.State != TcpActiveConnectState.SynSent
            || transaction.State != TcpActiveConnectState.SynSent
            || transaction.RemoteInitialSequenceNumber != ServerInitialSequence
            || transaction.RemoteNextSequenceNumber != ServerInitialSequence + 1)
        {
            return false;
        }

        Span<byte> finalAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpActiveConnectStatus ackStatus = transaction.TryWriteFinalAck(
            finalAck,
            windowSize: 0x4000,
            checksum: 0,
            out TcpActiveConnectTransitionResult ackResult);
        if (ackStatus != TcpActiveConnectStatus.Success
            || !ackResult.Succeeded
            || ackResult.State != TcpActiveConnectState.Established
            || ackResult.BytesWritten != TcpHeader.MinHeaderLength
            || transaction.State != TcpActiveConnectState.Established)
        {
            return false;
        }

        if (!ParseHeader(finalAck, out TcpHeader ackHeader)
            || ackHeader.SourcePort != LocalPort
            || ackHeader.DestinationPort != RemotePort
            || ackHeader.SequenceNumber != ClientInitialSequence + 1
            || ackHeader.AcknowledgementNumber != ServerInitialSequence + 1
            || ackHeader.Flags != TcpActiveConnectTransaction.FlagAck)
        {
            return false;
        }

        TcpActiveConnectStatus invalidStateStatus = transaction.Timeout(
            out TcpActiveConnectTransitionResult invalidStateResult);
        return invalidStateStatus == TcpActiveConnectStatus.InvalidState
            && !invalidStateResult.Succeeded
            && invalidStateResult.State == TcpActiveConnectState.Established
            && transaction.State == TcpActiveConnectState.Established;
    }

    private static bool TestActiveConnectRejectsBadSynAck()
    {
        return RejectsSynAck(
                ServerInitialSequence,
                ClientInitialSequence + 2,
                TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck,
                RemotePort,
                LocalPort,
                TcpActiveConnectStatus.WrongAcknowledgementNumber)
            && RejectsSynAck(
                ServerInitialSequence,
                ClientInitialSequence + 1,
                TcpActiveConnectTransaction.FlagAck,
                RemotePort,
                LocalPort,
                TcpActiveConnectStatus.InvalidFlags)
            && RejectsSynAck(
                ServerInitialSequence + 1,
                ClientInitialSequence + 1,
                TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck,
                RemotePort,
                LocalPort,
                TcpActiveConnectStatus.WrongSequenceNumber)
            && RejectsSynAck(
                ServerInitialSequence,
                ClientInitialSequence + 1,
                TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck,
                LocalPort,
                RemotePort,
                TcpActiveConnectStatus.UnexpectedPorts)
            && RejectsTruncatedSynAck()
            && RejectsFinalAckBeforeSynAck()
            && RejectsTooSmallSynBuffer();
    }

    private static bool TestActiveConnectTimesOut()
    {
        TcpActiveConnectTransaction transaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        if (transaction.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpActiveConnectTransitionResult _) != TcpActiveConnectStatus.Success)
        {
            return false;
        }

        TcpActiveConnectStatus status = transaction.Timeout(
            out TcpActiveConnectTransitionResult timeoutResult);
        if (status != TcpActiveConnectStatus.Timeout
            || timeoutResult.Succeeded
            || timeoutResult.State != TcpActiveConnectState.Failed
            || transaction.State != TcpActiveConnectState.Failed)
        {
            return false;
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int synAckBytes = WriteServerSegment(
            synAck,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpActiveConnectStatus receiveAfterTimeoutStatus = transaction.ReceiveSynAck(
            synAck.Slice(0, synAckBytes),
            out TcpActiveConnectTransitionResult receiveAfterTimeoutResult);

        TcpActiveConnectTransaction closedTransaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        TcpActiveConnectStatus closedTimeoutStatus = closedTransaction.Timeout(
            out TcpActiveConnectTransitionResult closedTimeoutResult);

        return receiveAfterTimeoutStatus == TcpActiveConnectStatus.InvalidState
            && receiveAfterTimeoutResult.State == TcpActiveConnectState.Failed
            && closedTimeoutStatus == TcpActiveConnectStatus.InvalidState
            && closedTimeoutResult.State == TcpActiveConnectState.Closed
            && closedTransaction.State == TcpActiveConnectState.Closed;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpActiveConnectTransitionResult defaultResult = default;
        TcpActiveConnectTransitionResult failedResult =
            TcpActiveConnectTransitionResult.Fail(
                TcpActiveConnectStatus.InvalidState,
                TcpActiveConnectState.Closed,
                PacketParseStatus.Success,
                localNextSequenceNumber: 11,
                remoteNextSequenceNumber: 0);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpActiveConnectStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == TcpActiveConnectStatus.InvalidState
            && failedResult.State == TcpActiveConnectState.Closed
            && failedResult.LocalNextSequenceNumber == 11
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpActiveConnectTransitionResult>();
    }

    private static bool RejectsSynAck(
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags,
        ushort sourcePort,
        ushort destinationPort,
        TcpActiveConnectStatus expectedStatus)
    {
        TcpActiveConnectTransaction transaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence,
            ServerInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        if (transaction.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpActiveConnectTransitionResult _) != TcpActiveConnectStatus.Success)
        {
            return false;
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int synAckBytes = WriteServerSegment(
            synAck,
            sequenceNumber,
            acknowledgementNumber,
            flags,
            sourcePort,
            destinationPort);
        TcpActiveConnectStatus status = transaction.ReceiveSynAck(
            synAck.Slice(0, synAckBytes),
            out TcpActiveConnectTransitionResult result);

        return status == expectedStatus
            && !result.Succeeded
            && result.State == TcpActiveConnectState.Failed
            && transaction.State == TcpActiveConnectState.Failed;
    }

    private static bool RejectsTruncatedSynAck()
    {
        TcpActiveConnectTransaction transaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        if (transaction.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpActiveConnectTransitionResult _) != TcpActiveConnectStatus.Success)
        {
            return false;
        }

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength - 1];
        TcpActiveConnectStatus status = transaction.ReceiveSynAck(
            synAck,
            out TcpActiveConnectTransitionResult result);
        return status == TcpActiveConnectStatus.SegmentParseFailed
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.State == TcpActiveConnectState.Failed
            && transaction.State == TcpActiveConnectState.Failed;
    }

    private static bool RejectsFinalAckBeforeSynAck()
    {
        TcpActiveConnectTransaction transaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        Span<byte> finalAck = stackalloc byte[TcpHeader.MinHeaderLength];

        return transaction.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpActiveConnectTransitionResult _) == TcpActiveConnectStatus.Success
            && transaction.TryWriteFinalAck(
                finalAck,
                windowSize: 0x4000,
                checksum: 0,
                out TcpActiveConnectTransitionResult finalAckResult) == TcpActiveConnectStatus.InvalidState
            && finalAckResult.State == TcpActiveConnectState.SynSent
            && transaction.State == TcpActiveConnectState.SynSent;
    }

    private static bool RejectsTooSmallSynBuffer()
    {
        TcpActiveConnectTransaction transaction = TcpActiveConnectTransaction.Create(
            LocalPort,
            RemotePort,
            ClientInitialSequence);
        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength - 1];

        return transaction.TryWriteSyn(
                syn,
                windowSize: 0x4000,
                checksum: 0,
                out TcpActiveConnectTransitionResult result) == TcpActiveConnectStatus.BufferTooSmall
            && !result.Succeeded
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.State == TcpActiveConnectState.Closed
            && transaction.State == TcpActiveConnectState.Closed;
    }

    private static int WriteServerSegment(
        Span<byte> segment,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags)
    {
        return WriteServerSegment(
            segment,
            sequenceNumber,
            acknowledgementNumber,
            flags,
            RemotePort,
            LocalPort);
    }

    private static int WriteServerSegment(
        Span<byte> segment,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags,
        ushort sourcePort,
        ushort destinationPort)
    {
        PacketParseStatus status = TcpHeader.TryWrite(
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
