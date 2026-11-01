namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetReceivedFrameMetadata
{
    public VirtioNetReceivedFrameMetadata(VirtioNetHeader virtioHeader, EthernetHeader ethernetHeader, int virtioHeaderLength, int ethernetFrameOffset, int ethernetFrameLength, int ethernetPayloadOffset, int ethernetPayloadLength)
    {
        VirtioHeader = virtioHeader;
        EthernetHeader = ethernetHeader;
        VirtioHeaderLength = virtioHeaderLength;
        EthernetFrameOffset = ethernetFrameOffset;
        EthernetFrameLength = ethernetFrameLength;
        EthernetPayloadOffset = ethernetPayloadOffset;
        EthernetPayloadLength = ethernetPayloadLength;
    }

    public VirtioNetHeader VirtioHeader { get; }
    public EthernetHeader EthernetHeader { get; }
    public int VirtioHeaderLength { get; }
    public int EthernetFrameOffset { get; }
    public int EthernetFrameLength { get; }
    public int EthernetPayloadOffset { get; }
    public int EthernetPayloadLength { get; }
}
