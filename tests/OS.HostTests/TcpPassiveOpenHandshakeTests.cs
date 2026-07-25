using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpPassiveOpenHandshakeTests
{
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint ClientInitialSequence = 0x10203040;
    private const uint ServerInitialSequence = 0xa0b0c0d0;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp passive handshake transitions listen-synack-established", TestListenSynAckEstablished, ref failures);
        Run("net.tcp passive handshake rejects wrong final ack", TestRejectsWrongFinalAck, ref failures);
        Run("net.tcp passive handshake rejects invalid syn", TestRejectsInvalidSyn, ref failures);
        Run("net.tcp passive handshake default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP passive-open handshake host tests failed: " + failures);
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

    private static bool TestListenSynAckEstablished()
    {
        TcpPassiveOpenHandshake handshake = TcpPassiveOpenHandshake.Create(
            LocalPort,
            RemotePort,
            ServerInitialSequence);

        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        int synBytes = WriteClientSegment(
            syn,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn,
            sourcePort: RemotePort,
            destinationPort: LocalPort);

        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpPassiveOpenHandshakeStatus synStatus = handshake.ReceiveSynAndWriteSynAck(
            syn.Slice(0, synBytes),
            synAck,
            windowSize: 0x4000,
            checksum: 0x2222,
            out TcpPassiveOpenHandshakeTransitionResult synResult);
        if (synStatus != TcpPassiveOpenHandshakeStatus.Success
            || !synResult.Succeeded
            || synResult.State != TcpPassiveOpenHandshakeState.SynReceived
            || synResult.BytesWritten != TcpHeader.MinHeaderLength
            || handshake.State != TcpPassiveOpenHandshakeState.SynReceived
            || handshake.RemoteInitialSequenceNumber != ClientInitialSequence
            || handshake.RemoteNextSequenceNumber != ClientInitialSequence + 1
            || handshake.LocalNextSequenceNumber != ServerInitialSequence + 1)
        {
            return false;
        }

        if (!ParseHeader(synAck, out TcpHeader synAckHeader)
            || synAckHeader.SourcePort != LocalPort
            || synAckHeader.DestinationPort != RemotePort
            || synAckHeader.SequenceNumber != ServerInitialSequence
            || synAckHeader.AcknowledgementNumber != ClientInitialSequence + 1
            || synAckHeader.Flags != (TcpPassiveOpenHandshake.FlagSyn | TcpPassiveOpenHandshake.FlagAck)
            || synAckHeader.WindowSize != 0x4000
            || synAckHeader.Checksum != 0x2222)
        {
            return false;
        }

        Span<byte> finalAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int finalAckBytes = WriteClientSegment(
            finalAck,
            ClientInitialSequence + 1,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagAck,
            sourcePort: RemotePort,
            destinationPort: LocalPort);

        TcpPassiveOpenHandshakeStatus ackStatus = handshake.ReceiveFinalAck(
            finalAck.Slice(0, finalAckBytes),
            out TcpPassiveOpenHandshakeTransitionResult ackResult);
        if (ackStatus != TcpPassiveOpenHandshakeStatus.Success
            || !ackResult.Succeeded
            || ackResult.State != TcpPassiveOpenHandshakeState.Established
            || handshake.State != TcpPassiveOpenHandshakeState.Established)
        {
            return false;
        }

        TcpPassiveOpenHandshakeStatus invalidStateStatus = handshake.ReceiveFinalAck(
            finalAck.Slice(0, finalAckBytes),
            out TcpPassiveOpenHandshakeTransitionResult invalidStateResult);

        return invalidStateStatus == TcpPassiveOpenHandshakeStatus.InvalidState
            && !invalidStateResult.Succeeded
            && invalidStateResult.State == TcpPassiveOpenHandshakeState.Established
            && handshake.State == TcpPassiveOpenHandshakeState.Established;
    }

    private static bool TestRejectsWrongFinalAck()
    {
        TcpPassiveOpenHandshake handshake = CreateSynReceivedHandshake();

        Span<byte> finalAck = stackalloc byte[TcpHeader.MinHeaderLength];
        int finalAckBytes = WriteClientSegment(
            finalAck,
            ClientInitialSequence + 1,
            ServerInitialSequence + 2,
            TcpPassiveOpenHandshake.FlagAck,
            sourcePort: RemotePort,
            destinationPort: LocalPort);

        TcpPassiveOpenHandshakeStatus status = handshake.ReceiveFinalAck(
            finalAck.Slice(0, finalAckBytes),
            out TcpPassiveOpenHandshakeTransitionResult result);

        return status == TcpPassiveOpenHandshakeStatus.WrongAcknowledgementNumber
            && !result.Succeeded
            && result.State == TcpPassiveOpenHandshakeState.Failed
            && handshake.State == TcpPassiveOpenHandshakeState.Failed;
    }

    private static bool TestRejectsInvalidSyn()
    {
        bool missingSynRejected = RejectsSyn(
            TcpPassiveOpenHandshake.FlagAck,
            sourcePort: RemotePort,
            destinationPort: LocalPort,
            TcpPassiveOpenHandshakeStatus.InvalidFlags);

        bool wrongSourcePortRejected = RejectsSyn(
            TcpPassiveOpenHandshake.FlagSyn,
            sourcePort: 50000,
            destinationPort: LocalPort,
            TcpPassiveOpenHandshakeStatus.UnexpectedPorts);

        bool wrongDestinationPortRejected = RejectsSyn(
            TcpPassiveOpenHandshake.FlagSyn,
            sourcePort: RemotePort,
            destinationPort: LocalPort + 1u > ushort.MaxValue ? ushort.MaxValue : (ushort)(LocalPort + 1),
            TcpPassiveOpenHandshakeStatus.UnexpectedPorts);

        TcpPassiveOpenHandshake handshake = TcpPassiveOpenHandshake.Create(
            LocalPort,
            RemotePort,
            ServerInitialSequence);
        Span<byte> ack = stackalloc byte[TcpHeader.MinHeaderLength];
        int ackBytes = WriteClientSegment(
            ack,
            ClientInitialSequence + 1,
            ServerInitialSequence + 1,
            TcpPassiveOpenHandshake.FlagAck,
            sourcePort: RemotePort,
            destinationPort: LocalPort);
        TcpPassiveOpenHandshakeStatus invalidStateStatus = handshake.ReceiveFinalAck(
            ack.Slice(0, ackBytes),
            out TcpPassiveOpenHandshakeTransitionResult invalidStateResult);

        return missingSynRejected
            && wrongSourcePortRejected
            && wrongDestinationPortRejected
            && invalidStateStatus == TcpPassiveOpenHandshakeStatus.InvalidState
            && invalidStateResult.State == TcpPassiveOpenHandshakeState.Listen
            && handshake.State == TcpPassiveOpenHandshakeState.Listen;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpPassiveOpenHandshakeTransitionResult defaultResult = default;
        TcpPassiveOpenHandshakeTransitionResult failedResult =
            TcpPassiveOpenHandshakeTransitionResult.Fail(
                TcpPassiveOpenHandshakeStatus.InvalidState,
                TcpPassiveOpenHandshakeState.Listen,
                PacketParseStatus.Success,
                localNextSequenceNumber: ServerInitialSequence,
                remoteNextSequenceNumber: 0);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpPassiveOpenHandshakeStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == TcpPassiveOpenHandshakeStatus.InvalidState
            && failedResult.State == TcpPassiveOpenHandshakeState.Listen
            && failedResult.LocalNextSequenceNumber == ServerInitialSequence
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpPassiveOpenHandshakeTransitionResult>();
    }

    private static TcpPassiveOpenHandshake CreateSynReceivedHandshake()
    {
        TcpPassiveOpenHandshake handshake = TcpPassiveOpenHandshake.Create(
            LocalPort,
            RemotePort,
            ServerInitialSequence);

        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        int synBytes = WriteClientSegment(
            syn,
            ClientInitialSequence,
            acknowledgementNumber: 0,
            TcpPassiveOpenHandshake.FlagSyn,
            sourcePort: RemotePort,
            destinationPort: LocalPort);
        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpPassiveOpenHandshakeStatus status = handshake.ReceiveSynAndWriteSynAck(
            syn.Slice(0, synBytes),
            synAck,
            windowSize: 0x4000,
            checksum: 0,
            out TcpPassiveOpenHandshakeTransitionResult _);

        return status == TcpPassiveOpenHandshakeStatus.Success
            ? handshake
            : throw new InvalidOperationException("failed to create SYN_RECEIVED handshake");
    }

    private static bool RejectsSyn(
        byte flags,
        ushort sourcePort,
        ushort destinationPort,
        TcpPassiveOpenHandshakeStatus expectedStatus)
    {
        TcpPassiveOpenHandshake handshake = TcpPassiveOpenHandshake.Create(
            LocalPort,
            RemotePort,
            ServerInitialSequence);

        Span<byte> syn = stackalloc byte[TcpHeader.MinHeaderLength];
        int synBytes = WriteClientSegment(
            syn,
            ClientInitialSequence,
            flags == TcpPassiveOpenHandshake.FlagSyn ? 0u : ServerInitialSequence + 1,
            flags,
            sourcePort,
            destinationPort);
        Span<byte> synAck = stackalloc byte[TcpHeader.MinHeaderLength];

        TcpPassiveOpenHandshakeStatus status = handshake.ReceiveSynAndWriteSynAck(
            syn.Slice(0, synBytes),
            synAck,
            windowSize: 0x4000,
            checksum: 0,
            out TcpPassiveOpenHandshakeTransitionResult result);

        return status == expectedStatus
            && !result.Succeeded
            && result.State == TcpPassiveOpenHandshakeState.Failed
            && handshake.State == TcpPassiveOpenHandshakeState.Failed;
    }

    private static int WriteClientSegment(
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
