namespace OS.Net;

public readonly struct TcpConnectionTableResult
{
    private TcpConnectionTableResult(TcpConnectionTableStatus status, int count, int capacity, int requiredCapacity, int entryIndex, TcpConnectionTableEntry entry)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RequiredCapacity = requiredCapacity;
        EntryIndex = entryIndex;
        Entry = entry;
    }

    public TcpConnectionTableStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpConnectionTableStatus.Success;
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
    public TcpConnectionTableEntry Entry
    {
        get;
    }

    public static TcpConnectionTableResult Create(TcpConnectionTableStatus status, int count, int capacity, int requiredCapacity, int entryIndex, TcpConnectionTableEntry entry)
    {
        return new TcpConnectionTableResult(status, count, capacity, requiredCapacity, entryIndex, entry);
    }
}
