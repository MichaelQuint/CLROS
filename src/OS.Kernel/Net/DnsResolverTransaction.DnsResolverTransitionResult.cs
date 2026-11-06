namespace OS.Net;

public readonly struct DnsResolverTransitionResult
{
    private DnsResolverTransitionResult(DnsResolverStatus status, DnsResolverState state, DnsMessageStatus messageStatus, int bytesWritten, Ipv4Address address)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        MessageStatus = messageStatus;
        BytesWritten = bytesWritten;
        Address = address;
    }

    public DnsResolverStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == DnsResolverStatus.Success;
    public DnsResolverState State
    {
        get;
    }
    public DnsMessageStatus MessageStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public Ipv4Address Address
    {
        get;
    }

    public static DnsResolverTransitionResult Success(DnsResolverState state, int bytesWritten, Ipv4Address address)
    {
        return new DnsResolverTransitionResult(DnsResolverStatus.Success, state, DnsMessageStatus.Success, bytesWritten, address);
    }

    public static DnsResolverTransitionResult Fail(DnsResolverStatus status, DnsResolverState state, DnsMessageStatus messageStatus)
    {
        return new DnsResolverTransitionResult(status, state, messageStatus, 0, default);
    }
}
