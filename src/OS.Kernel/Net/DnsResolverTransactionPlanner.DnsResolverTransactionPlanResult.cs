namespace OS.Net;
using OS.Std.Binary;

public readonly struct DnsResolverTransactionPlanResult
{
    private DnsResolverTransactionPlanResult(DnsResolverTransactionPlanStatus status, DnsUdpQueryFrameResult queryFrameResult, UdpReceiveDispatchResult udpResult, DnsUdpReceiveFrameResult receiveFrameResult, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, PacketParseStatus packetStatus, UdpReceiveDispatchStatus udpStatus, ushort transactionId, int queryNameLength, uint queryNameFingerprint, Ipv4Address answerAddress, uint answerTtlSeconds, int bytesWritten, int responseSize, Ipv4Address sourceServer)
    {
        IsInitialized = true;
        Status = status;
        QueryFrameResult = queryFrameResult;
        UdpResult = udpResult;
        ReceiveFrameResult = receiveFrameResult;
        ResolverStatus = resolverStatus;
        MessageStatus = messageStatus;
        PacketStatus = packetStatus;
        UdpStatus = udpStatus;
        TransactionId = transactionId;
        QueryNameLength = queryNameLength;
        QueryNameFingerprint = queryNameFingerprint;
        AnswerAddress = answerAddress;
        AnswerTtlSeconds = answerTtlSeconds;
        BytesWritten = bytesWritten;
        ResponseSize = responseSize;
        SourceServer = sourceServer;
    }

    public DnsResolverTransactionPlanStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == DnsResolverTransactionPlanStatus.Success;
    public DnsUdpQueryFrameResult QueryFrameResult { get; }
    public UdpReceiveDispatchResult UdpResult { get; }
    public DnsUdpReceiveFrameResult ReceiveFrameResult { get; }
    public DnsResolverStatus ResolverStatus { get; }
    public DnsMessageStatus MessageStatus { get; }
    public PacketParseStatus PacketStatus { get; }
    public UdpReceiveDispatchStatus UdpStatus { get; }
    public ushort TransactionId { get; }
    public int QueryNameLength { get; }
    public uint QueryNameFingerprint { get; }
    public Ipv4Address AnswerAddress { get; }
    public uint AnswerTtlSeconds { get; }
    public int BytesWritten { get; }
    public int ResponseSize { get; }
    public Ipv4Address SourceServer { get; }

    public static DnsResolverTransactionPlanResult QuerySuccess(DnsUdpQueryFrameResult queryFrameResult, ushort transactionId, int queryNameLength, uint queryNameFingerprint)
    {
        return new DnsResolverTransactionPlanResult(DnsResolverTransactionPlanStatus.Success, queryFrameResult, default, default, queryFrameResult.ResolverStatus, queryFrameResult.MessageStatus, queryFrameResult.PacketStatus, UdpReceiveDispatchStatus.Success, transactionId, queryNameLength, queryNameFingerprint, default, 0, queryFrameResult.BytesWritten, 0, default);
    }

    public static DnsResolverTransactionPlanResult ReceiveSuccess(UdpReceiveDispatchResult udpResult, DnsUdpReceiveFrameResult receiveFrameResult, ushort transactionId, int queryNameLength, uint queryNameFingerprint, uint answerTtlSeconds)
    {
        return new DnsResolverTransactionPlanResult(DnsResolverTransactionPlanStatus.Success, default, udpResult, receiveFrameResult, receiveFrameResult.ResolverStatus, receiveFrameResult.MessageStatus, receiveFrameResult.PacketStatus, receiveFrameResult.UdpStatus, transactionId, queryNameLength, queryNameFingerprint, receiveFrameResult.ResolvedAddress, answerTtlSeconds, 0, receiveFrameResult.DnsPayloadLength, udpResult.SourceAddress);
    }

    public static DnsResolverTransactionPlanResult Fail(DnsResolverTransactionPlanStatus status, DnsUdpQueryFrameResult queryFrameResult, UdpReceiveDispatchResult udpResult, DnsUdpReceiveFrameResult receiveFrameResult, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, PacketParseStatus packetStatus, UdpReceiveDispatchStatus udpStatus, ushort transactionId, int queryNameLength, uint queryNameFingerprint, int responseSize, Ipv4Address sourceServer)
    {
        return new DnsResolverTransactionPlanResult(status, queryFrameResult, udpResult, receiveFrameResult, resolverStatus, messageStatus, packetStatus, udpStatus, transactionId, queryNameLength, queryNameFingerprint, default, 0, 0, responseSize, sourceServer);
    }
}
