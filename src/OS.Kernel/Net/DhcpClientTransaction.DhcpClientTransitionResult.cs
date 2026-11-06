namespace OS.Net;

public readonly struct DhcpClientTransitionResult
{
    private DhcpClientTransitionResult(DhcpClientStatus status, DhcpClientState state, DhcpMessageStatus messageStatus, int bytesWritten)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        MessageStatus = messageStatus;
        BytesWritten = bytesWritten;
    }

    public DhcpClientStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == DhcpClientStatus.Success;
    public DhcpClientState State
    {
        get;
    }
    public DhcpMessageStatus MessageStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }

    public static DhcpClientTransitionResult Success(DhcpClientState state, int bytesWritten)
    {
        return new DhcpClientTransitionResult(DhcpClientStatus.Success, state, DhcpMessageStatus.Success, bytesWritten);
    }

    public static DhcpClientTransitionResult Fail(DhcpClientStatus status, DhcpClientState state, DhcpMessageStatus messageStatus)
    {
        return new DhcpClientTransitionResult(status, state, messageStatus, 0);
    }
}
