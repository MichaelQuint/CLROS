namespace OS.Net;
public readonly struct TcpEstablishedStreamReadResult
{
    private TcpEstablishedStreamReadResult(TcpEstablishedStreamStatus status, int bytesRead, int unreadBytes, uint acknowledgementNumber)
    {
        IsInitialized = true;
        Status = status;
        BytesRead = bytesRead;
        UnreadBytes = unreadBytes;
        AcknowledgementNumber = acknowledgementNumber;
    }

    public bool IsInitialized { get; }
    public TcpEstablishedStreamStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == TcpEstablishedStreamStatus.Success;
    public int BytesRead { get; }
    public int UnreadBytes { get; }
    public uint AcknowledgementNumber { get; }

    public static TcpEstablishedStreamReadResult Success(int bytesRead, int unreadBytes, uint acknowledgementNumber)
    {
        return new TcpEstablishedStreamReadResult(TcpEstablishedStreamStatus.Success, bytesRead, unreadBytes, acknowledgementNumber);
    }

    public static TcpEstablishedStreamReadResult Fail(TcpEstablishedStreamStatus status, int unreadBytes, uint acknowledgementNumber)
    {
        return new TcpEstablishedStreamReadResult(status, bytesRead: 0, unreadBytes, acknowledgementNumber);
    }
}
