using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpIpv4FramePlannerTests
{
    private const ushort LocalPort = 49170;
    private const ushort RemotePort = 80;
    private const uint ClientInitialSequence = 0x10203040;
    private const uint ServerInitialSequence = 0x50607080;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp.ipv4.frame writes syn frame", TestWritesSynFrame, ref failures);
        Run("net.tcp.ipv4.frame writes syn checksum", TestWritesSynChecksum, ref failures);
        Run("net.tcp.ipv4.frame accepts syn ack frame", TestAcceptsSynAckFrame, ref failures);
        Run("net.tcp.ipv4.frame writes final ack frame", TestWritesFinalAckFrame, ref failures);
        Run("net.tcp.ipv4.frame writes final ack checksum", TestWritesFinalAckChecksum, ref failures);
        Run("net.tcp.ipv4.frame rejects wrong peer", TestRejectsWrongPeer, ref failures);
        Run("net.tcp.ipv4.frame rejects protocol and bounds", TestRejectsProtocolAndBounds, ref failures);
        Run("net.tcp.ipv4.frame rejects bad tcp checksum", TestRejectsBadTcpChecksum, ref failures);
        Run("net.tcp.ipv4.frame rejects bad tcp syn ack", TestRejectsBadTcpSynAck, ref failures);
        Run("net.tcp.ipv4.frame rejects identity and short output", TestRejectsIdentityAndShortOutput, ref failures);
        Run("net.tcp.ipv4.frame default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.tcp mac validation uses shared helpers", TestMacValidationUsesSharedHelpers, ref failures);
        Run("net.tcp zero mac validation uses shared helper", TestZeroMacValidationUsesSharedHelper, ref failures);
        Run(
            "net.tcp ipv4 frame planners use shared buffer operations",
            TestTcpIpv4FramePlannersUseSharedBufferOperations,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP IPv4 frame planner host tests failed: " + failures);
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

    private static bool TestWritesSynFrame()
    {
        TcpIpv4FramePlanner planner = CreatePlanner();
        byte[] frame = new byte[TcpIpv4FramePlanner.HeaderBytes];

        TcpIpv4FramePlanStatus status = planner.TryWriteSynFrame(
            frame,
            out TcpIpv4FramePlanResult result);

        if (status != TcpIpv4FramePlanStatus.Success
            || !result.Succeeded
            || result.State != TcpActiveConnectState.SynSent
            || planner.State != TcpActiveConnectState.SynSent
            || result.BytesWritten != TcpIpv4FramePlanner.HeaderBytes
            || result.TcpSegmentOffset != EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            || result.TcpSegmentLength != TcpHeader.MinHeaderLength
            || result.LocalNextSequenceNumber != ClientInitialSequence + 1)
        {
            return false;
        }

        return TryParseTcpFrame(
                frame.AsSpan(0, result.BytesWritten),
                out EthernetHeader ethernet,
                out Ipv4Header ipv4,
                out TcpHeader tcp)
            && ethernet.Source == LocalMac()
            && ethernet.Destination == RemoteMac()
            && ethernet.EtherType == EthernetHeader.EtherTypeIpv4
            && ipv4.Source == LocalAddress()
            && ipv4.Destination == RemoteAddress()
            && ipv4.Protocol == TcpIpv4FramePlanner.ProtocolTcp
            && ipv4.TimeToLive == 42
            && ipv4.Identification == 0x220
            && tcp.SourcePort == LocalPort
            && tcp.DestinationPort == RemotePort
            && tcp.SequenceNumber == ClientInitialSequence
            && tcp.AcknowledgementNumber == 0
            && tcp.Flags == TcpActiveConnectTransaction.FlagSyn
            && tcp.WindowSize == 0x3000
            && tcp.Checksum != 0;
    }

    private static bool TestWritesSynChecksum()
    {
        TcpIpv4FramePlanner planner = CreatePlanner();
        byte[] frame = new byte[TcpIpv4FramePlanner.HeaderBytes];

        TcpIpv4FramePlanStatus status = planner.TryWriteSynFrame(
            frame,
            out TcpIpv4FramePlanResult result);

        if (!TryParseTcpFrame(
                frame.AsSpan(0, result.BytesWritten),
                out EthernetHeader _,
                out Ipv4Header ipv4,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> tcpSegment))
        {
            return false;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);

        return status == TcpIpv4FramePlanStatus.Success
            && result.Succeeded
            && result.TcpHeader.Checksum == tcp.Checksum
            && tcp.Checksum != 0
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == TcpHeader.MinHeaderLength;
    }

    private static bool TestAcceptsSynAckFrame()
    {
        TcpIpv4FramePlanner planner = PreparedPlanner();
        byte[] synAck = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);

        TcpIpv4FramePlanStatus status = planner.ReceiveSynAckFrame(
            synAck,
            out TcpIpv4FramePlanResult result);

        return status == TcpIpv4FramePlanStatus.Success
            && result.Succeeded
            && result.State == TcpActiveConnectState.SynSent
            && planner.State == TcpActiveConnectState.SynSent
            && result.TcpStatus == TcpActiveConnectStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.RemoteInitialSequenceNumber == ServerInitialSequence
            && result.RemoteNextSequenceNumber == ServerInitialSequence + 1
            && result.LocalNextSequenceNumber == ClientInitialSequence + 1
            && result.EthernetHeader.Source == RemoteMac()
            && result.EthernetHeader.Destination == LocalMac()
            && result.Ipv4Header.Source == RemoteAddress()
            && result.Ipv4Header.Destination == LocalAddress()
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpSegmentOffset == EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            && result.TcpSegmentLength == TcpHeader.MinHeaderLength;
    }

    private static bool TestWritesFinalAckFrame()
    {
        TcpIpv4FramePlanner planner = PreparedPlanner();
        byte[] synAck = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        if (planner.ReceiveSynAckFrame(
                synAck,
                out TcpIpv4FramePlanResult _) != TcpIpv4FramePlanStatus.Success)
        {
            return false;
        }

        byte[] finalAck = new byte[TcpIpv4FramePlanner.HeaderBytes];
        TcpIpv4FramePlanStatus status = planner.TryWriteFinalAckFrame(
            finalAck,
            out TcpIpv4FramePlanResult result);

        return status == TcpIpv4FramePlanStatus.Success
            && result.Succeeded
            && result.State == TcpActiveConnectState.Established
            && planner.State == TcpActiveConnectState.Established
            && result.BytesWritten == TcpIpv4FramePlanner.HeaderBytes
            && TryParseTcpFrame(
                finalAck.AsSpan(0, result.BytesWritten),
                out EthernetHeader ethernet,
                out Ipv4Header ipv4,
                out TcpHeader tcp)
            && ethernet.Source == LocalMac()
            && ethernet.Destination == RemoteMac()
            && ipv4.Protocol == TcpIpv4FramePlanner.ProtocolTcp
            && tcp.SourcePort == LocalPort
            && tcp.DestinationPort == RemotePort
            && tcp.SequenceNumber == ClientInitialSequence + 1
            && tcp.AcknowledgementNumber == ServerInitialSequence + 1
            && tcp.Flags == TcpActiveConnectTransaction.FlagAck;
    }

    private static bool TestWritesFinalAckChecksum()
    {
        TcpIpv4FramePlanner planner = PreparedEstablishedPlanner();
        byte[] finalAck = new byte[TcpIpv4FramePlanner.HeaderBytes];

        TcpIpv4FramePlanStatus status = planner.TryWriteFinalAckFrame(
            finalAck,
            out TcpIpv4FramePlanResult result);

        if (!TryParseTcpFrame(
                finalAck.AsSpan(0, result.BytesWritten),
                out EthernetHeader _,
                out Ipv4Header ipv4,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> tcpSegment))
        {
            return false;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);

        return status == TcpIpv4FramePlanStatus.Success
            && result.Succeeded
            && result.State == TcpActiveConnectState.Established
            && result.TcpHeader.Checksum == tcp.Checksum
            && tcp.Checksum != 0
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == TcpHeader.MinHeaderLength;
    }

    private static bool TestRejectsWrongPeer()
    {
        TcpIpv4FramePlanner wrongMacPlanner = PreparedPlanner();
        byte[] wrongMac = CreateServerSynAckFrame(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x0B, 0xAD),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpIpv4FramePlanStatus wrongMacStatus = wrongMacPlanner.ReceiveSynAckFrame(
            wrongMac,
            out TcpIpv4FramePlanResult wrongMacResult);

        TcpIpv4FramePlanner wrongIpPlanner = PreparedPlanner();
        byte[] wrongIp = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            new Ipv4Address(203, 0, 113, 80),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpIpv4FramePlanStatus wrongIpStatus = wrongIpPlanner.ReceiveSynAckFrame(
            wrongIp,
            out TcpIpv4FramePlanResult wrongIpResult);

        return wrongMacStatus == TcpIpv4FramePlanStatus.WrongRemoteMac
            && !wrongMacResult.Succeeded
            && wrongMacResult.PacketStatus == PacketParseStatus.Success
            && wrongMacPlanner.State == TcpActiveConnectState.SynSent
            && wrongIpStatus == TcpIpv4FramePlanStatus.WrongRemoteAddress
            && !wrongIpResult.Succeeded
            && wrongIpResult.PacketStatus == PacketParseStatus.Success
            && wrongIpResult.Ipv4Header.Source == new Ipv4Address(203, 0, 113, 80)
            && wrongIpPlanner.State == TcpActiveConnectState.SynSent;
    }

    private static bool TestRejectsProtocolAndBounds()
    {
        TcpIpv4FramePlanner shortPlanner = PreparedPlanner();
        byte[] shortFrame = new byte[EthernetHeader.HeaderLength - 1];
        TcpIpv4FramePlanStatus shortStatus = shortPlanner.ReceiveSynAckFrame(
            shortFrame,
            out TcpIpv4FramePlanResult shortResult);

        TcpIpv4FramePlanner etherTypePlanner = PreparedPlanner();
        byte[] wrongEtherType = new byte[EthernetHeader.HeaderLength];
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(
            wrongEtherType,
            new EthernetHeader(LocalMac(), RemoteMac(), EthernetHeader.EtherTypeArp));
        TcpIpv4FramePlanStatus etherTypeStatus = etherTypePlanner.ReceiveSynAckFrame(
            wrongEtherType,
            out TcpIpv4FramePlanResult etherTypeResult);

        TcpIpv4FramePlanner protocolPlanner = PreparedPlanner();
        byte[] wrongProtocol = CreateServerFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            protocol: Ipv4Header.ProtocolUdp,
            sourcePort: RemotePort,
            destinationPort: LocalPort,
            sequenceNumber: ServerInitialSequence,
            acknowledgementNumber: ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpIpv4FramePlanStatus protocolStatus = protocolPlanner.ReceiveSynAckFrame(
            wrongProtocol,
            out TcpIpv4FramePlanResult protocolResult);

        return shortStatus == TcpIpv4FramePlanStatus.FrameParseFailed
            && shortResult.PacketStatus == PacketParseStatus.BufferTooSmall
            && shortPlanner.State == TcpActiveConnectState.SynSent
            && ethernetStatus == PacketParseStatus.Success
            && etherTypeStatus == TcpIpv4FramePlanStatus.UnsupportedEtherType
            && etherTypeResult.PacketStatus == PacketParseStatus.UnsupportedEtherType
            && etherTypePlanner.State == TcpActiveConnectState.SynSent
            && protocolStatus == TcpIpv4FramePlanStatus.UnsupportedIpProtocol
            && protocolResult.PacketStatus == PacketParseStatus.UnsupportedIpProtocol
            && protocolPlanner.State == TcpActiveConnectState.SynSent;
    }

    private static bool TestRejectsBadTcpChecksum()
    {
        TcpIpv4FramePlanner planner = PreparedPlanner();
        byte[] synAck = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        synAck[EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpChecksumOffset] ^= 0x01;

        TcpIpv4FramePlanStatus status = planner.ReceiveSynAckFrame(
            synAck,
            out TcpIpv4FramePlanResult result);

        return status == TcpIpv4FramePlanStatus.BadTcpChecksum
            && !result.Succeeded
            && result.TcpStatus == TcpActiveConnectStatus.Success
            && result.PacketStatus == PacketParseStatus.BadChecksum
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpSegmentOffset == EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            && result.TcpSegmentLength == TcpHeader.MinHeaderLength
            && planner.State == TcpActiveConnectState.SynSent;
    }

    private static bool TestRejectsBadTcpSynAck()
    {
        TcpIpv4FramePlanner wrongAckPlanner = PreparedPlanner();
        byte[] wrongAck = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 2,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpIpv4FramePlanStatus wrongAckStatus = wrongAckPlanner.ReceiveSynAckFrame(
            wrongAck,
            out TcpIpv4FramePlanResult wrongAckResult);

        TcpIpv4FramePlanner wrongPortPlanner = PreparedPlanner();
        byte[] wrongPort = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            sourcePort: RemotePort + 1,
            destinationPort: LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        TcpIpv4FramePlanStatus wrongPortStatus = wrongPortPlanner.ReceiveSynAckFrame(
            wrongPort,
            out TcpIpv4FramePlanResult wrongPortResult);

        return wrongAckStatus == TcpIpv4FramePlanStatus.WrongAcknowledgementNumber
            && !wrongAckResult.Succeeded
            && wrongAckResult.TcpStatus == TcpActiveConnectStatus.WrongAcknowledgementNumber
            && wrongAckResult.TcpHeader.AcknowledgementNumber == ClientInitialSequence + 2
            && wrongAckPlanner.State == TcpActiveConnectState.Failed
            && wrongPortStatus == TcpIpv4FramePlanStatus.WrongTcpPorts
            && !wrongPortResult.Succeeded
            && wrongPortResult.TcpStatus == TcpActiveConnectStatus.UnexpectedPorts
            && wrongPortResult.TcpHeader.SourcePort == RemotePort + 1
            && wrongPortPlanner.State == TcpActiveConnectState.Failed;
    }

    private static bool TestRejectsIdentityAndShortOutput()
    {
        TcpIpv4FramePlanner invalidLocal = TcpIpv4FramePlanner.Create(
            new TcpIpv4FramePlannerInfo(
                default,
                RemoteMac(),
                LocalAddress(),
                RemoteAddress(),
                LocalPort,
                RemotePort,
                ClientInitialSequence));
        byte[] frame = PoisonedFrame(TcpIpv4FramePlanner.HeaderBytes);
        TcpIpv4FramePlanStatus invalidStatus = invalidLocal.TryWriteSynFrame(
            frame,
            out TcpIpv4FramePlanResult invalidResult);

        TcpIpv4FramePlanner shortOutput = CreatePlanner();
        byte[] shortFrame = PoisonedFrame(TcpIpv4FramePlanner.HeaderBytes);
        TcpIpv4FramePlanStatus shortStatus = shortOutput.TryWriteSynFrame(
            shortFrame.AsSpan(0, shortFrame.Length - 1),
            out TcpIpv4FramePlanResult shortResult);

        return invalidStatus == TcpIpv4FramePlanStatus.InvalidLocalIdentity
            && !invalidResult.Succeeded
            && invalidResult.State == TcpActiveConnectState.Closed
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte
            && shortStatus == TcpIpv4FramePlanStatus.FrameBufferTooSmall
            && !shortResult.Succeeded
            && shortResult.PacketStatus == PacketParseStatus.BufferTooSmall
            && shortOutput.State == TcpActiveConnectState.Closed
            && shortFrame[0] == PoisonByte
            && shortFrame[^1] == PoisonByte;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpIpv4FramePlannerInfo info = new(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            LocalPort,
            RemotePort,
            ClientInitialSequence,
            ServerInitialSequence,
            requiresRemoteInitialSequenceNumber: true,
            windowSize: 0x3000,
            timeToLive: 42,
            identification: 0x220,
            tcpChecksum: 0x1111);
        TcpIpv4FramePlanResult defaultResult = default;
        TcpIpv4FramePlanResult success = TcpIpv4FramePlanResult.Success(
            TcpActiveConnectState.SynSent,
            TcpActiveConnectStatus.Success,
            PacketParseStatus.Success,
            default,
            default,
            default,
            TcpIpv4FramePlanner.HeaderBytes,
            EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
            TcpHeader.MinHeaderLength,
            info,
            ClientInitialSequence + 1,
            ServerInitialSequence,
            ServerInitialSequence + 1);
        TcpIpv4FramePlanResult failure = TcpIpv4FramePlanResult.Fail(
            TcpIpv4FramePlanStatus.FrameBufferTooSmall,
            TcpActiveConnectState.Closed,
            TcpActiveConnectStatus.BufferTooSmall,
            PacketParseStatus.BufferTooSmall,
            default,
            default,
            default,
            tcpSegmentOffset: 0,
            tcpSegmentLength: 0,
            info,
            ClientInitialSequence,
            remoteInitialSequenceNumber: 0,
            remoteNextSequenceNumber: 0);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpIpv4FramePlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.State == TcpActiveConnectState.SynSent
            && success.TcpStatus == TcpActiveConnectStatus.Success
            && success.PacketStatus == PacketParseStatus.Success
            && success.BytesWritten == TcpIpv4FramePlanner.HeaderBytes
            && success.TcpSegmentOffset == EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            && success.TcpSegmentLength == TcpHeader.MinHeaderLength
            && success.LocalMac == LocalMac()
            && success.RemoteMac == RemoteMac()
            && success.LocalAddress == LocalAddress()
            && success.RemoteAddress == RemoteAddress()
            && success.LocalPort == LocalPort
            && success.RemotePort == RemotePort
            && success.TimeToLive == 42
            && success.Identification == 0x220
            && success.LocalNextSequenceNumber == ClientInitialSequence + 1
            && success.RemoteInitialSequenceNumber == ServerInitialSequence
            && success.RemoteNextSequenceNumber == ServerInitialSequence + 1
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == TcpIpv4FramePlanStatus.FrameBufferTooSmall
            && failure.State == TcpActiveConnectState.Closed
            && failure.TcpStatus == TcpActiveConnectStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.BytesWritten == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpIpv4FramePlanResult>();
    }

    private static bool TestMacValidationUsesSharedHelpers()
    {
        string root = HostToolProcess.FindRepositoryRoot();

        return TcpPlannerSourceUsesSharedBroadcastHelper(ReadKernelNetSource(root, "TcpIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedBroadcastHelper(ReadKernelNetSource(root, "TcpPassiveIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedBroadcastHelper(
                ReadKernelNetSource(root, "TcpEstablishedStreamIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedBroadcastHelper(
                ReadKernelNetSource(root, "TcpConnectionCloseIpv4FramePlanner.cs"));
    }

    private static bool TestZeroMacValidationUsesSharedHelper()
    {
        string root = HostToolProcess.FindRepositoryRoot();

        return TcpPlannerSourceUsesSharedZeroHelper(ReadKernelNetSource(root, "TcpIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedZeroHelper(ReadKernelNetSource(root, "TcpPassiveIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedZeroHelper(
                ReadKernelNetSource(root, "TcpEstablishedStreamIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedZeroHelper(
                ReadKernelNetSource(root, "TcpConnectionCloseIpv4FramePlanner.cs"));
    }

    private static bool TestTcpIpv4FramePlannersUseSharedBufferOperations()
    {
        string root = HostToolProcess.FindRepositoryRoot();

        return TcpPlannerSourceUsesSharedBufferOperations(ReadKernelNetSource(root, "TcpIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedBufferOperations(ReadKernelNetSource(root, "TcpPassiveIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedBufferOperations(
                ReadKernelNetSource(root, "TcpEstablishedStreamIpv4FramePlanner.cs"))
            && TcpPlannerSourceUsesSharedBufferOperations(
                ReadKernelNetSource(root, "TcpConnectionCloseIpv4FramePlanner.cs"));
    }

    private static string ReadKernelNetSource(string root, string fileName)
    {
        return File.ReadAllText(Path.Combine(root, "src", "OS.Kernel", "Net", fileName));
    }

    private static bool TcpPlannerSourceUsesSharedBroadcastHelper(string source)
    {
        return source.Contains(".IsBroadcast", StringComparison.Ordinal)
            && !source.Contains("private static bool IsBroadcast(MacAddress", StringComparison.Ordinal);
    }

    private static bool TcpPlannerSourceUsesSharedZeroHelper(string source)
    {
        return source.Contains("LocalMac.IsZero", StringComparison.Ordinal)
            && source.Contains("RemoteMac.IsZero", StringComparison.Ordinal)
            && !source.Contains("LocalMac == default", StringComparison.Ordinal)
            && !source.Contains("RemoteMac == default", StringComparison.Ordinal)
            && !source.Contains("private static bool IsZero(MacAddress", StringComparison.Ordinal);
    }

    private static bool TcpPlannerSourceUsesSharedBufferOperations(string source)
    {
        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(", StringComparison.Ordinal)
            && source.Contains(
                "frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, tcpSegment.Length)",
                StringComparison.Ordinal)
            && source.Contains("return PacketParseStatus.BufferTooSmall;", StringComparison.Ordinal)
            && !source.Contains(
                "tcpSegment.CopyTo(frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, tcpSegment.Length))",
                StringComparison.Ordinal);
    }

    private static TcpIpv4FramePlanner PreparedPlanner()
    {
        TcpIpv4FramePlanner planner = CreatePlanner();
        byte[] frame = new byte[TcpIpv4FramePlanner.HeaderBytes];
        if (planner.TryWriteSynFrame(
                frame,
                out TcpIpv4FramePlanResult result) != TcpIpv4FramePlanStatus.Success
            || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to prepare TCP IPv4 frame planner.");
        }

        return planner;
    }

    private static TcpIpv4FramePlanner PreparedEstablishedPlanner()
    {
        TcpIpv4FramePlanner planner = PreparedPlanner();
        byte[] synAck = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        if (planner.ReceiveSynAckFrame(
                synAck,
                out TcpIpv4FramePlanResult result) != TcpIpv4FramePlanStatus.Success
            || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to establish TCP IPv4 frame planner.");
        }

        return planner;
    }

    private static TcpIpv4FramePlanner CreatePlanner()
    {
        return TcpIpv4FramePlanner.Create(
            new TcpIpv4FramePlannerInfo(
                LocalMac(),
                RemoteMac(),
                LocalAddress(),
                RemoteAddress(),
                LocalPort,
                RemotePort,
                ClientInitialSequence,
                ServerInitialSequence,
                requiresRemoteInitialSequenceNumber: true,
                windowSize: 0x3000,
                timeToLive: 42,
                identification: 0x220,
                tcpChecksum: 0x1111));
    }

    private static byte[] CreateServerSynAckFrame(
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
        return CreateServerFrame(
            sourceMac,
            destinationMac,
            sourceAddress,
            destinationAddress,
            TcpIpv4FramePlanner.ProtocolTcp,
            sourcePort,
            destinationPort,
            sequenceNumber,
            acknowledgementNumber,
            flags);
    }

    private static byte[] CreateServerFrame(
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
        byte[] frame = new byte[TcpIpv4FramePlanner.HeaderBytes];
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

        if (protocol == TcpIpv4FramePlanner.ProtocolTcp)
        {
            Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
                sourceAddress,
                destinationAddress,
                TcpIpv4FramePlanner.ProtocolTcp,
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
            identification: 0x661,
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

    private static void WriteTcpChecksum(Span<byte> segment, ushort checksum)
    {
        segment[TcpChecksumOffset] = (byte)(checksum >> 8);
        segment[TcpChecksumOffset + 1] = (byte)checksum;
    }

    private const byte PoisonByte = 0xA5;
    private const int TcpChecksumOffset = 16;

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x22, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x22, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 170);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 80);
    }
}
