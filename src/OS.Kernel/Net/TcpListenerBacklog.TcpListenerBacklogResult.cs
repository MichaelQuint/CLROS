namespace OS.Net;

public readonly struct TcpListenerBacklogResult
{
    private TcpListenerBacklogResult(TcpListenerBacklogStatus status, int count, int capacity, int requiredCapacity, int entryIndex, TcpListenerBacklogConnection connection)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RequiredCapacity = requiredCapacity;
        EntryIndex = entryIndex;
        Connection = connection;
    }

    public TcpListenerBacklogStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpListenerBacklogStatus.Success;
    public int Count
    {
        get;
    }
    public int Capacity
    {
        get;
    }
    public int RequiredCapacity
    {
        get;
    }
    public int EntryIndex
    {
        get;
    }
    public TcpListenerBacklogConnection Connection
    {
        get;
    }

    public static TcpListenerBacklogResult Create(TcpListenerBacklogStatus status, int count, int capacity, int requiredCapacity, int entryIndex, TcpListenerBacklogConnection connection)
    {
        return new TcpListenerBacklogResult(status, count, capacity, requiredCapacity, entryIndex, connection);
    }
}
