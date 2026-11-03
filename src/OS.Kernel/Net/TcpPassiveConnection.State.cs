namespace OS.Net;

public static unsafe partial class TcpPassiveConnection
{
    /// <summary>Noncopyable caller-retained metadata, initially all zero.
    /// Pending state retains a table handle even if the first TX is full.
    /// Faulted state requires CancelPending. Established hands the table handle
    /// to the later stream/close consumer; that consumer owns final release and
    /// backlog removal. Observations alone grant no transport/socket authority.</summary>

    public struct State
    {
        internal ulong Self, Started, LastNow, Deadline, NextRetry;
        internal TcpConnectionTable.Owner* Table;
        internal TcpConnectionTable.Handle Handle;
        internal TcpPassiveIpv4FramePlanner Planner;
        internal MacAddress LocalMac, RemoteMac;
        internal Operations Io;
        internal byte Phase, Attempts;
        internal ulong Duplicates;
        internal ushort PeerMss, PeerWindow;
        internal uint WindowSequence, WindowAcknowledgement;
        public readonly bool Pending => Phase == 1;
        public readonly bool RequiresCleanup => Phase == 2;
        public readonly bool Established => Phase == 3;
        public readonly bool Retired => Phase == 4;
        public readonly byte SuccessfulAttempts => Attempts;
        public readonly ulong DuplicateReplies => Duplicates;
        public readonly ulong DeadlineNanoseconds => Deadline;
        public readonly ulong NextRetryNanoseconds => NextRetry;
        public readonly TcpConnectionTable.Handle Connection => Handle;
        public readonly ushort PeerMaximumSegmentSize => PeerMss;
        public readonly ushort PeerAdvertisedWindow => PeerWindow;
    }
}
