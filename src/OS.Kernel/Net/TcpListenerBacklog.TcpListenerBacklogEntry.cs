namespace OS.Net;

public struct TcpListenerBacklogEntry
{
    private TcpListenerBacklogEntry(TcpListenerBacklogEntryState state, TcpListenerBacklogConnection connection, ulong enqueueOrder)
    {
        State = state;
        Connection = connection;
        EnqueueOrder = enqueueOrder;
    }

    public TcpListenerBacklogEntryState State
    {
        get;
    }
    public TcpListenerBacklogConnection Connection
    {
        get;
    }
    public ulong EnqueueOrder
    {
        get;
    }
    public bool IsPendingAccept => State == TcpListenerBacklogEntryState.PendingAccept;

    internal static TcpListenerBacklogEntry Pending(TcpListenerBacklogConnection connection, ulong enqueueOrder)
    {
        return new TcpListenerBacklogEntry(TcpListenerBacklogEntryState.PendingAccept, connection, enqueueOrder);
    }
}
