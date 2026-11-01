namespace OS.Net;
using OS.Drivers;

public readonly struct NetworkReceiveDispatchResult
{
    private NetworkReceiveDispatchResult(NetworkReceiveDispatchStatus status, VirtioNetReceiveFrameClassificationResult classifierResult, PacketParseStatus payloadPacketStatus, ArpPacket arpPacket, Ipv4Header ipv4Header, int ipv4PayloadOffset, int ipv4PayloadLength)
    {
        IsInitialized = true;
        Status = status;
        ClassifierResult = classifierResult;
        PayloadPacketStatus = payloadPacketStatus;
        ArpPacket = arpPacket;
        Ipv4Header = ipv4Header;
        Ipv4PayloadOffset = ipv4PayloadOffset;
        Ipv4PayloadLength = ipv4PayloadLength;
    }

    public NetworkReceiveDispatchStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == NetworkReceiveDispatchStatus.Success;
    public VirtioNetReceiveFrameClassificationResult ClassifierResult { get; }
    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus => ClassifierResult.Status;
    public PacketParseStatus ClassifierPacketStatus => ClassifierResult.PacketStatus;
    public PacketParseStatus PayloadPacketStatus { get; }
    public EthernetHeader EthernetHeader => ClassifierResult.EthernetHeader;
    public EthernetPayloadKind PayloadKind => ClassifierResult.PayloadKind;
    public int PayloadOffset => ClassifierResult.PayloadOffset;
    public int PayloadLength => ClassifierResult.PayloadLength;
    public ArpPacket ArpPacket { get; }
    public Ipv4Header Ipv4Header { get; }
    public int Ipv4PayloadOffset { get; }
    public int Ipv4PayloadLength { get; }

    public static NetworkReceiveDispatchResult Arp(VirtioNetReceiveFrameClassificationResult classifierResult, ArpPacket arpPacket)
    {
        return new NetworkReceiveDispatchResult(NetworkReceiveDispatchStatus.Success, classifierResult, PacketParseStatus.Success, arpPacket, default, 0, 0);
    }

    public static NetworkReceiveDispatchResult Ipv4(VirtioNetReceiveFrameClassificationResult classifierResult, Ipv4Header ipv4Header, int ipv4PayloadOffset, int ipv4PayloadLength)
    {
        return new NetworkReceiveDispatchResult(NetworkReceiveDispatchStatus.Success, classifierResult, PacketParseStatus.Success, default, ipv4Header, ipv4PayloadOffset, ipv4PayloadLength);
    }

    public static NetworkReceiveDispatchResult Fail(NetworkReceiveDispatchStatus status, VirtioNetReceiveFrameClassificationResult classifierResult, PacketParseStatus payloadPacketStatus)
    {
        return new NetworkReceiveDispatchResult(status, classifierResult, payloadPacketStatus, default, default, 0, 0);
    }
}
