namespace OS.Net;
public struct ArpNeighborResolutionTransaction
{
    public const int RequestFrameLength = EthernetHeader.HeaderLength + ArpPacket.PacketLength;
    private static MacAddress BroadcastMac => new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
    private static MacAddress ZeroMac => new MacAddress(0x00, 0x00, 0x00, 0x00, 0x00, 0x00);

    private ArpNeighborResolutionTransaction(MacAddress localMac, Ipv4Address localAddress, Ipv4Address targetAddress)
    {
        LocalMac = localMac;
        LocalAddress = localAddress;
        TargetAddress = targetAddress;
        State = ArpNeighborResolutionState.Initial;
        ResolvedMac = default;
    }

    public ArpNeighborResolutionState State { get; private set; }
    public MacAddress LocalMac { get; }
    public Ipv4Address LocalAddress { get; }
    public Ipv4Address TargetAddress { get; }
    public MacAddress ResolvedMac { get; private set; }

    public static ArpNeighborResolutionTransaction Create(MacAddress localMac, Ipv4Address localAddress, Ipv4Address targetAddress)
    {
        return new ArpNeighborResolutionTransaction(localMac, localAddress, targetAddress);
    }

    public ArpNeighborResolutionStatus TryWriteRequest(Span<byte> frame, out ArpNeighborResolutionResult result)
    {
        if (State != ArpNeighborResolutionState.Initial)
        {
            result = ArpNeighborResolutionResult.Fail(ArpNeighborResolutionStatus.InvalidState, State, PacketParseStatus.Success);
            return result.Status;
        }

        if (frame.Length < RequestFrameLength)
        {
            result = ArpNeighborResolutionResult.Fail(ArpNeighborResolutionStatus.BufferTooSmall, State, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        EthernetHeader ethernet = new EthernetHeader(BroadcastMac, LocalMac, EthernetHeader.EtherTypeArp);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            ArpNeighborResolutionStatus status = MapWriteStatus(ethernetStatus);
            result = ArpNeighborResolutionResult.Fail(status, State, ethernetStatus);
            return status;
        }

        ArpPacket request = new ArpPacket(ArpPacket.OperationRequest, LocalMac, LocalAddress, ZeroMac, TargetAddress);
        PacketParseStatus arpStatus = ArpPacket.TryWrite(frame.Slice(EthernetHeader.HeaderLength, ArpPacket.PacketLength), request, out int arpBytesWritten);
        if (arpStatus != PacketParseStatus.Success || arpBytesWritten != ArpPacket.PacketLength)
        {
            ArpNeighborResolutionStatus status = MapWriteStatus(arpStatus);
            result = ArpNeighborResolutionResult.Fail(status, State, arpStatus);
            return status;
        }

        State = ArpNeighborResolutionState.RequestSent;
        result = ArpNeighborResolutionResult.Success(State, RequestFrameLength, default);
        return result.Status;
    }

    public ArpNeighborResolutionStatus ReceiveReply(ReadOnlySpan<byte> frame, out ArpNeighborResolutionResult result)
    {
        if (State != ArpNeighborResolutionState.RequestSent)
        {
            result = ArpNeighborResolutionResult.Fail(ArpNeighborResolutionStatus.InvalidState, State, PacketParseStatus.Success);
            return result.Status;
        }

        PacketParseStatus ethernetStatus = EthernetHeader.TryDispatch(frame, out EthernetHeader ethernet, out EthernetPayloadKind payloadKind, out ReadOnlySpan<byte> payload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            return Fail(MapParseStatus(ethernetStatus), ethernetStatus, out result);
        }

        if (payloadKind != EthernetPayloadKind.Arp)
        {
            return Fail(ArpNeighborResolutionStatus.UnsupportedEtherType, PacketParseStatus.UnsupportedEtherType, out result);
        }

        PacketParseStatus arpStatus = ArpPacket.TryParse(payload, out ArpPacket reply);
        if (arpStatus != PacketParseStatus.Success)
        {
            return Fail(MapParseStatus(arpStatus), arpStatus, out result);
        }

        if (reply.Operation != ArpPacket.OperationReply)
        {
            return Fail(ArpNeighborResolutionStatus.UnexpectedOperation, PacketParseStatus.Success, out result);
        }

        if (reply.SenderAddress != TargetAddress)
        {
            return Fail(ArpNeighborResolutionStatus.SenderAddressMismatch, PacketParseStatus.Success, out result);
        }

        if (reply.TargetAddress != LocalAddress)
        {
            return Fail(ArpNeighborResolutionStatus.TargetAddressMismatch, PacketParseStatus.Success, out result);
        }

        if (reply.TargetMac != LocalMac)
        {
            return Fail(ArpNeighborResolutionStatus.TargetMacMismatch, PacketParseStatus.Success, out result);
        }

        if (ethernet.Destination != LocalMac)
        {
            return Fail(ArpNeighborResolutionStatus.EthernetDestinationMismatch, PacketParseStatus.Success, out result);
        }

        if (ethernet.Source != reply.SenderMac)
        {
            return Fail(ArpNeighborResolutionStatus.EthernetSourceMismatch, PacketParseStatus.Success, out result);
        }

        ResolvedMac = reply.SenderMac;
        State = ArpNeighborResolutionState.Resolved;
        result = ArpNeighborResolutionResult.Success(State, 0, ResolvedMac);
        return result.Status;
    }

    public ArpNeighborResolutionStatus Timeout(out ArpNeighborResolutionResult result)
    {
        if (State != ArpNeighborResolutionState.RequestSent)
        {
            result = ArpNeighborResolutionResult.Fail(ArpNeighborResolutionStatus.InvalidState, State, PacketParseStatus.Success);
            return result.Status;
        }

        State = ArpNeighborResolutionState.Failed;
        result = ArpNeighborResolutionResult.Fail(ArpNeighborResolutionStatus.Timeout, State, PacketParseStatus.Success);
        return result.Status;
    }

    private ArpNeighborResolutionStatus Fail(ArpNeighborResolutionStatus status, PacketParseStatus packetStatus, out ArpNeighborResolutionResult result)
    {
        State = ArpNeighborResolutionState.Failed;
        result = ArpNeighborResolutionResult.Fail(status, State, packetStatus);
        return status;
    }

    private static ArpNeighborResolutionStatus MapWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall ? ArpNeighborResolutionStatus.BufferTooSmall : ArpNeighborResolutionStatus.FrameWriteFailed;
    }

    private static ArpNeighborResolutionStatus MapParseStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.UnsupportedEtherType ? ArpNeighborResolutionStatus.UnsupportedEtherType : ArpNeighborResolutionStatus.FrameParseFailed;
    }
}
