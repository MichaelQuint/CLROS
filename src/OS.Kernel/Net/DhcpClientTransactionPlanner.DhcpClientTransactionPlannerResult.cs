namespace OS.Net;

public readonly struct DhcpClientTransactionPlannerResult
{
    private DhcpClientTransactionPlannerResult(DhcpClientTransactionPlannerStatus status, DhcpClientState state, DhcpUdpFrameStatus frameStatus, DhcpClientStatus clientStatus, DhcpMessageStatus messageStatus, PacketParseStatus packetStatus, UdpReceiveDispatchStatus udpStatus, DhcpLeaseConfigurationStatus leaseStatus, uint transactionId, MacAddress clientMac, Ipv4Address offeredAddress, Ipv4Address serverIdentifier, DhcpLeaseConfiguration leaseConfiguration, int bytesWritten, int payloadOffset, int payloadLength)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        FrameStatus = frameStatus;
        ClientStatus = clientStatus;
        MessageStatus = messageStatus;
        PacketStatus = packetStatus;
        UdpStatus = udpStatus;
        LeaseStatus = leaseStatus;
        TransactionId = transactionId;
        ClientMac = clientMac;
        OfferedAddress = offeredAddress;
        ServerIdentifier = serverIdentifier;
        LeaseConfiguration = leaseConfiguration;
        BytesWritten = bytesWritten;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public DhcpClientTransactionPlannerStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == DhcpClientTransactionPlannerStatus.Success;
    public DhcpClientState State
    {
        get;
    }
    public DhcpUdpFrameStatus FrameStatus
    {
        get;
    }
    public DhcpClientStatus ClientStatus
    {
        get;
    }
    public DhcpMessageStatus MessageStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public UdpReceiveDispatchStatus UdpStatus
    {
        get;
    }
    public DhcpLeaseConfigurationStatus LeaseStatus
    {
        get;
    }
    public uint TransactionId
    {
        get;
    }
    public MacAddress ClientMac
    {
        get;
    }
    public Ipv4Address OfferedAddress
    {
        get;
    }
    public Ipv4Address ServerIdentifier
    {
        get;
    }
    public DhcpLeaseConfiguration LeaseConfiguration
    {
        get;
    }
    public int BytesWritten
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

    public static DhcpClientTransactionPlannerResult Success(DhcpClientState state, uint transactionId, MacAddress clientMac, Ipv4Address offeredAddress, Ipv4Address serverIdentifier, DhcpLeaseConfiguration leaseConfiguration, int bytesWritten, int payloadOffset, int payloadLength)
    {
        return new DhcpClientTransactionPlannerResult(DhcpClientTransactionPlannerStatus.Success, state, DhcpUdpFrameStatus.Success, DhcpClientStatus.Success, DhcpMessageStatus.Success, PacketParseStatus.Success, UdpReceiveDispatchStatus.Success, DhcpLeaseConfigurationStatus.Success, transactionId, clientMac, offeredAddress, serverIdentifier, leaseConfiguration, bytesWritten, payloadOffset, payloadLength);
    }

    public static DhcpClientTransactionPlannerResult Fail(DhcpClientTransactionPlannerStatus status, DhcpClientState state, DhcpUdpFrameStatus frameStatus, DhcpClientStatus clientStatus, DhcpMessageStatus messageStatus, PacketParseStatus packetStatus, UdpReceiveDispatchStatus udpStatus, DhcpLeaseConfigurationStatus leaseStatus, uint transactionId, MacAddress clientMac, Ipv4Address offeredAddress, Ipv4Address serverIdentifier, int bytesWritten, int payloadOffset, int payloadLength)
    {
        return new DhcpClientTransactionPlannerResult(status, state, frameStatus, clientStatus, messageStatus, packetStatus, udpStatus, leaseStatus, transactionId, clientMac, offeredAddress, serverIdentifier, default, bytesWritten, payloadOffset, payloadLength);
    }
}
