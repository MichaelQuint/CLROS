namespace OS.Net;
public readonly struct TcpEstablishedStreamSendResult
{
    private TcpEstablishedStreamSendResult(TcpEstablishedStreamStatus status, PacketParseStatus packetStatus, int segmentBytesWritten, int payloadBytesWritten, uint sequenceNumber, uint acknowledgementNumber, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        PacketStatus = packetStatus;
        SegmentBytesWritten = segmentBytesWritten;
        PayloadBytesWritten = payloadBytesWritten;
        SequenceNumber = sequenceNumber;
        AcknowledgementNumber = acknowledgementNumber;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public bool IsInitialized { get; }
    public TcpEstablishedStreamStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == TcpEstablishedStreamStatus.Success;
    public PacketParseStatus PacketStatus { get; }
    public int SegmentBytesWritten { get; }
    public int PayloadBytesWritten { get; }
    public uint SequenceNumber { get; }
    public uint AcknowledgementNumber { get; }
    public uint LocalNextSequenceNumber { get; }
    public uint RemoteNextSequenceNumber { get; }

    public static TcpEstablishedStreamSendResult Success(int segmentBytesWritten, int payloadBytesWritten, uint sequenceNumber, uint acknowledgementNumber, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamSendResult(TcpEstablishedStreamStatus.Success, PacketParseStatus.Success, segmentBytesWritten, payloadBytesWritten, sequenceNumber, acknowledgementNumber, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpEstablishedStreamSendResult Fail(TcpEstablishedStreamStatus status, PacketParseStatus packetStatus, uint sequenceNumber, uint acknowledgementNumber, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamSendResult(status, packetStatus, segmentBytesWritten: 0, payloadBytesWritten: 0, sequenceNumber, acknowledgementNumber, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
