namespace OS.Net;

public struct TcpConnectionTableEntry
{
    // A persistent receiver binds this exact generation. It must detach and
    // scrub its buffer before the closed table entry can be released.
    internal ulong ReceiveOwnerAddress;
    internal ulong SendOwnerAddress;
    private TcpConnectionTableEntry(TcpConnectionLifecycleState state, TcpConnectionKey key, TcpConnectionSequenceMetadata sequence)
    {
        State = state;
        Key = key;
        Sequence = sequence;
    }

    public TcpConnectionLifecycleState State
    {
        get;
    }
    public TcpConnectionKey Key
    {
        get;
    }
    public TcpConnectionSequenceMetadata Sequence
    {
        get;
    }

    internal readonly TcpConnectionTableEntry WithMetadata(TcpConnectionLifecycleState state, TcpConnectionSequenceMetadata sequence)
    {
        var updated = Create(state, Key, sequence);
        updated.ReceiveOwnerAddress = ReceiveOwnerAddress;
        updated.SendOwnerAddress = SendOwnerAddress;
        return updated;
    }

    public bool IsActive => State == TcpConnectionLifecycleState.SynReceived || State == TcpConnectionLifecycleState.Established || State == TcpConnectionLifecycleState.Closing;

    public static TcpConnectionTableEntry Create(TcpConnectionLifecycleState state, TcpConnectionKey key, TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(state, key, sequence);
    }

    internal static TcpConnectionTableEntry Established(TcpConnectionKey key, TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(TcpConnectionLifecycleState.Established, key, sequence);
    }

    internal static TcpConnectionTableEntry Closing(TcpConnectionKey key, TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(TcpConnectionLifecycleState.Closing, key, sequence);
    }

    internal static TcpConnectionTableEntry Closed(TcpConnectionKey key, TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(TcpConnectionLifecycleState.Closed, key, sequence);
    }
}
