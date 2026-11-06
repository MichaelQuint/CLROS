namespace OS.Drivers;

using OS.Net;

public readonly struct VirtioNetReceiveFrameClassificationResult
{
    private VirtioNetReceiveFrameClassificationResult(VirtioNetReceiveFrameClassifierStatus status, PacketParseStatus packetStatus, VirtioNetReceivedFrameMetadata virtioMetadata, EthernetHeader ethernetHeader, EthernetPayloadKind payloadKind, int payloadOffset, int payloadLength)
    {
        IsInitialized = true;
        Status = status;
        PacketStatus = packetStatus;
        VirtioMetadata = virtioMetadata;
        EthernetHeader = ethernetHeader;
        PayloadKind = payloadKind;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public VirtioNetReceiveFrameClassifierStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public VirtioNetReceivedFrameMetadata VirtioMetadata
    {
        get;
    }
    public EthernetHeader EthernetHeader
    {
        get;
    }
    public EthernetPayloadKind PayloadKind
    {
        get;
    }
    public int PayloadOffset
    {
        get;
    }
    public int PayloadLength
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveFrameClassifierStatus.Success;

    public static VirtioNetReceiveFrameClassificationResult Create(VirtioNetReceiveFrameClassifierStatus status, PacketParseStatus packetStatus, VirtioNetReceivedFrameMetadata virtioMetadata, EthernetHeader ethernetHeader, EthernetPayloadKind payloadKind, int payloadOffset, int payloadLength)
    {
        return new VirtioNetReceiveFrameClassificationResult(status, packetStatus, virtioMetadata, ethernetHeader, payloadKind, payloadOffset, payloadLength);
    }

    public static VirtioNetReceiveFrameClassificationResult Fail(VirtioNetReceiveFrameClassifierStatus status, PacketParseStatus packetStatus)
    {
        return new VirtioNetReceiveFrameClassificationResult(status, packetStatus, default, default, EthernetPayloadKind.Unsupported, 0, 0);
    }
}
