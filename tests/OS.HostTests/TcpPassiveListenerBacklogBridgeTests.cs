using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpPassiveListenerBacklogBridgeTests
{
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint ClientInitialSequence = 0x10203040;
    private const uint ServerInitialSequence = 0xa0b0c0d0;
    private const int TcpChecksumOffset = 16;

    [HostTest("net.tcp.passive.backlog enqueues established connection", HostLane.Fast, 0)]
    private static bool TestEnqueuesEstablishedConnection()
    {
        Span<TcpListenerBacklogEntry> entries = stackalloc TcpListenerBacklogEntry[2];
        TcpPassiveIpv4FramePlanResult passiveResult = CompleteHandshake();

        TcpPassiveListenerBacklogBridgeStatus status =
            TcpPassiveListenerBacklogBridge.EnqueueEstablished(
                entries,
                passiveResult,
                out TcpPassiveListenerBacklogBridgeResult result);

        return HostCheck.Result(status == TcpPassiveListenerBacklogBridgeStatus.Success
            && result.Succeeded
            && result.PassiveStatus == TcpPassiveIpv4FramePlanStatus.Success
            && result.TcpStatus == TcpPassiveOpenHandshakeStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.PassiveState == TcpPassiveOpenHandshakeState.Established
            && result.BacklogStatus == TcpListenerBacklogStatus.Success
            && result.BacklogCount == 1
            && result.BacklogCapacity == 2
            && result.RequiredCapacity == 1
            && result.EntryIndex == 0
            && result.Connection.LocalAddress == LocalAddress()
            && result.Connection.LocalPort == LocalPort
            && result.Connection.RemoteAddress == RemoteAddress()
            && result.Connection.RemotePort == RemotePort
            && result.Connection.LocalNextSequenceNumber == ServerInitialSequence + 1
            && result.Connection.RemoteNextSequenceNumber == ClientInitialSequence + 1
            && entries[0].State == TcpListenerBacklogEntryState.PendingAccept
            && entries[0].Connection.HasSameEndpoint(result.Connection)
            && entries[1].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.passive.backlog rejects failed handshake", HostLane.Fast, 1)]
    private static bool TestRejectsFailedHandshake()
    {
        Span<TcpListenerBacklogEntry> entries = stackalloc TcpListenerBacklogEntry[1];
        TcpListenerBacklogConnection existing = OtherConnection();
        TcpListenerBacklog.Enqueue(entries, existing);
        TcpPassiveIpv4FramePlanResult passiveResult = FailedFinalAck();

        TcpPassiveListenerBacklogBridgeStatus status =
            TcpPassiveListenerBacklogBridge.EnqueueEstablished(
                entries,
                passiveResult,
                out TcpPassiveListenerBacklogBridgeResult result);

        return HostCheck.Result(status == TcpPassiveListenerBacklogBridgeStatus.PassiveOpenFailed
            && !result.Succeeded
            && result.PassiveStatus == TcpPassiveIpv4FramePlanStatus.WrongAcknowledgementNumber
            && result.TcpStatus == TcpPassiveOpenHandshakeStatus.WrongAcknowledgementNumber
            && result.BacklogStatus == TcpListenerBacklogStatus.Success
            && result.BacklogCount == 1
            && result.BacklogCapacity == 1
            && entries[0].State == TcpListenerBacklogEntryState.PendingAccept
            && entries[0].Connection.HasSameEndpoint(existing));
    }

    [HostTest("net.tcp.passive.backlog rejects non-established result", HostLane.Fast, 2)]
    private static bool TestRejectsNonEstablishedResult()
    {
        Span<TcpListenerBacklogEntry> entries = stackalloc TcpListenerBacklogEntry[1];
        TcpPassiveIpv4FramePlanResult passiveResult = ReceiveSynOnly();

        TcpPassiveListenerBacklogBridgeStatus status =
            TcpPassiveListenerBacklogBridge.EnqueueEstablished(
                entries,
                passiveResult,
                out TcpPassiveListenerBacklogBridgeResult result);

        return HostCheck.Result(status == TcpPassiveListenerBacklogBridgeStatus.NotEstablished
            && !result.Succeeded
            && result.PassiveStatus == TcpPassiveIpv4FramePlanStatus.Success
            && result.PassiveState == TcpPassiveOpenHandshakeState.SynReceived
            && result.BacklogStatus == TcpListenerBacklogStatus.Success
            && result.BacklogCount == 0
            && result.BacklogCapacity == 1
            && entries[0].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.passive.backlog propagates backlog full", HostLane.Fast, 3)]
    private static bool TestPropagatesBacklogFull()
    {
        Span<TcpListenerBacklogEntry> entries = stackalloc TcpListenerBacklogEntry[1];
        TcpListenerBacklogConnection existing = OtherConnection();
        TcpListenerBacklog.Enqueue(entries, existing);
        TcpPassiveIpv4FramePlanResult passiveResult = CompleteHandshake();

        TcpPassiveListenerBacklogBridgeStatus status =
            TcpPassiveListenerBacklogBridge.EnqueueEstablished(
                entries,
                passiveResult,
                out TcpPassiveListenerBacklogBridgeResult result);

        return HostCheck.Result(status == TcpPassiveListenerBacklogBridgeStatus.BacklogEnqueueFailed
            && !result.Succeeded
            && result.BacklogStatus == TcpListenerBacklogStatus.BacklogFull
            && result.BacklogCount == 1
            && result.BacklogCapacity == 1
            && result.RequiredCapacity == 2
            && result.EntryIndex == TcpListenerBacklog.NoEntryIndex
            && entries[0].State == TcpListenerBacklogEntryState.PendingAccept
            && entries[0].Connection.HasSameEndpoint(existing));
    }

    [HostTest("net.tcp.passive.backlog propagates duplicate endpoint", HostLane.Fast, 4)]
    private static bool TestPropagatesDuplicateEndpoint()
    {
        Span<TcpListenerBacklogEntry> entries = stackalloc TcpListenerBacklogEntry[2];
        TcpPassiveIpv4FramePlanResult passiveResult = CompleteHandshake();
        TcpListenerBacklogConnection duplicate = new TcpListenerBacklogConnection(
            passiveResult.LocalAddress,
            passiveResult.LocalPort,
            passiveResult.RemoteAddress,
            passiveResult.RemotePort,
            passiveResult.LocalNextSequenceNumber,
            passiveResult.RemoteNextSequenceNumber);
        TcpListenerBacklog.Enqueue(entries, duplicate);

        TcpPassiveListenerBacklogBridgeStatus status =
            TcpPassiveListenerBacklogBridge.EnqueueEstablished(
                entries,
                passiveResult,
                out TcpPassiveListenerBacklogBridgeResult result);

        return HostCheck.Result(status == TcpPassiveListenerBacklogBridgeStatus.BacklogEnqueueFailed
            && !result.Succeeded
            && result.BacklogStatus == TcpListenerBacklogStatus.DuplicateEndpoint
            && result.BacklogCount == 1
            && result.BacklogCapacity == 2
            && result.RequiredCapacity == 2
            && result.EntryIndex == 0
            && entries[0].Connection.HasSameEndpoint(duplicate)
            && entries[1].State == TcpListenerBacklogEntryState.Free);
    }

    [HostTest("net.tcp.passive.backlog preserves no-socket boundary", HostLane.Fast, 5)]
    private static bool TestPreservesNoSocketBoundary()
    {
        return HostCheck.Result(TcpPassiveListenerBacklogBridge.RequiresCallerOwnedBacklogStorage
            && !TcpPassiveListenerBacklogBridge.ParsesPackets
            && !TcpPassiveListenerBacklogBridge.ExportsSocketHandles
            && !TcpPassiveListenerBacklogBridge.OwnsTimers
            && !TcpPassiveListenerBacklogBridge.RetransmitsSegments
            && !TcpPassiveListenerBacklogBridge.UsesVirtioNet
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpPassiveListenerBacklogBridgeResult>());
    }

    [HostTest("net.tcp.passive.backlog default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpPassiveListenerBacklogBridgeResult defaultResult = default;
        TcpListenerBacklogConnection connection = OtherConnection();
        TcpPassiveListenerBacklogBridgeResult failureResult =
            TcpPassiveListenerBacklogBridgeResult.Create(
                TcpPassiveListenerBacklogBridgeStatus.BacklogEnqueueFailed,
                default,
                TcpListenerBacklogStatus.BacklogFull,
                backlogCount: 1,
                backlogCapacity: 1,
                requiredCapacity: 2,
                TcpListenerBacklog.NoEntryIndex,
                connection);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpPassiveListenerBacklogBridgeStatus.Success
            && failureResult.IsInitialized
            && !failureResult.Succeeded
            && failureResult.Status == TcpPassiveListenerBacklogBridgeStatus.BacklogEnqueueFailed
            && failureResult.BacklogStatus == TcpListenerBacklogStatus.BacklogFull
            && failureResult.BacklogCount == 1
            && failureResult.BacklogCapacity == 1
            && failureResult.RequiredCapacity == 2
            && failureResult.EntryIndex == TcpListenerBacklog.NoEntryIndex
            && failureResult.Connection.HasSameEndpoint(connection)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpPassiveListenerBacklogBridgeResult>());
    }

    private static TcpPassiveIpv4FramePlanResult CompleteHandshake()
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
        if (status != TcpPassiveIpv4FramePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to complete passive TCP fixture.");
        }

        return result;
    }

    private static TcpPassiveIpv4FramePlanResult FailedFinalAck()
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
            ServerInitialSequence + 2,
            TcpPassiveOpenHandshake.FlagAck);

        planner.ReceiveFinalAckFrame(finalAck, out TcpPassiveIpv4FramePlanResult result);
        return result;
    }

    private static TcpPassiveIpv4FramePlanResult ReceiveSynOnly()
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
        if (status != TcpPassiveIpv4FramePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to prepare passive SYN fixture.");
        }

        return result;
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
            TcpPassiveIpv4FramePlanner.ProtocolTcp,
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

    private static TcpListenerBacklogConnection OtherConnection()
    {
        return new TcpListenerBacklogConnection(
            LocalAddress(),
            LocalPort,
            new Ipv4Address(198, 51, 100, 99),
            remotePort: 49199,
            localNextSequenceNumber: 10,
            remoteNextSequenceNumber: 20);
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
