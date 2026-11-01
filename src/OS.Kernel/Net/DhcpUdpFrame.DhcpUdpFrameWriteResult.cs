namespace OS.Net;
public readonly struct DhcpUdpFrameWriteResult
{
    private DhcpUdpFrameWriteResult(DhcpUdpFrameStatus status, DhcpMessageStatus dhcpStatus, PacketParseStatus packetStatus, int dhcpPayloadLength, int bytesWritten)
    {
        IsInitialized = true;
        Status = status;
        DhcpStatus = dhcpStatus;
        PacketStatus = packetStatus;
        DhcpPayloadLength = dhcpPayloadLength;
        BytesWritten = bytesWritten;
    }

    public DhcpUdpFrameStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == DhcpUdpFrameStatus.Success;
    public DhcpMessageStatus DhcpStatus { get; }
    public PacketParseStatus PacketStatus { get; }
    public int DhcpPayloadLength { get; }
    public int BytesWritten { get; }

    public static DhcpUdpFrameWriteResult Success(int dhcpPayloadLength, int bytesWritten)
    {
        return new DhcpUdpFrameWriteResult(DhcpUdpFrameStatus.Success, DhcpMessageStatus.Success, PacketParseStatus.Success, dhcpPayloadLength, bytesWritten);
    }

    public static DhcpUdpFrameWriteResult Fail(DhcpUdpFrameStatus status, DhcpMessageStatus dhcpStatus, PacketParseStatus packetStatus)
    {
        return new DhcpUdpFrameWriteResult(status, dhcpStatus, packetStatus, 0, 0);
    }
}
