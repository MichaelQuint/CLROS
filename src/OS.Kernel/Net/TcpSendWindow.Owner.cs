namespace OS.Net;

public static unsafe partial class TcpSendWindow
{
    /// <summary>Initially zero, noncopyable metadata. Retained bytes include
    /// both transmitted/unacknowledged and queued/unsent data. Observations are
    /// not socket authority. The receiver backing remains retained even if it is
    /// cleaned first; both attachments must detach before table release.</summary>

    public struct Owner
    {
        internal ulong Self;
        internal TcpConnectionTable.Owner* Table;
        internal TcpConnectionTable.Handle Handle;
        internal TcpReceiveWindow.Owner* Receiver;
        internal byte* Buffer;
        internal int CapacityValue, Offset, Count, Sent;
        internal uint Una, Next, WindowSequence, WindowAck;
        internal ushort PeerWindow, PeerMss, Mtu;
        internal void* Context;
        internal delegate*<void*, byte*, uint, byte> Send;
        internal void* ClockContext;
        internal delegate*<void*, ulong> Clock;
        internal ulong LastNow, HardDeadline, RetryAt, Rto, MinimumRto, MaximumRto;
        internal ulong SampleAt, SmoothedRtt, RttVariation, DuplicateAcks;
        internal ulong PersistAt, PersistDelay, PersistCount;
        internal uint SampleEnd;
        internal byte Timed, RetryCount, RetryLimit, Sampling, HasRtt, HandshakeRetried;
        internal byte CloseEnabled, WriteShutdown, FinPending;
        internal TcpConnectionCloseState CloseStateValue;
        internal ulong CloseLimit, CloseDeadline, TimeWaitDuration, TimeWaitDeadline;
        internal byte Phase, TimeoutError;
        internal fixed ushort FlightLengths[8];
        internal uint CongestionWindow, SlowStartThreshold, CongestionCredit;
        internal uint FlightAmbiguousEnd;
        internal ulong LastDataTransmit;
        internal byte FlightEnabled, FlightHead, FlightCount, FlightDuplicates, FastPending, FastRecovery, FlightAmbiguous, FlightHasSent;
        /// <summary>Configured flight metadata and congestion observations;
        /// these values grant no transmission, timestamp or buffer authority.</summary>
        public readonly uint CongestionWindowBytes => CongestionWindow;
        public readonly uint SlowStartThresholdBytes => SlowStartThreshold;
        public readonly byte OutstandingSegments => FlightCount;
        public readonly bool Active => Phase == 1;
        public readonly bool RequiresCleanup => Phase == 2;
        /// <summary>Actual retry or configured deadline exhaustion. The owner
        /// retains all backing until explicit timeout retirement and closed reap.</summary>

        public readonly bool TimedOut => TimeoutError == 1;
        public readonly bool Retired => Phase == 3;
        public readonly int BufferedBytes => Count;
        public readonly int InFlightBytes => Sent;
        public readonly int AvailableBytes => CapacityValue - Count;
        public readonly uint OldestUnacknowledgedSequence => Una;
        public readonly uint NextSequence => Next;
        public readonly ushort PeerMaximumSegmentSize => PeerMss;
        public readonly ushort PeerAdvertisedWindow => PeerWindow;
        public readonly ulong RetransmissionDeadlineNanoseconds => RetryAt;
        public readonly ulong RetransmissionTimeoutNanoseconds => Rto;
        public readonly byte Retransmissions => RetryCount;
        public readonly ulong DuplicateAcknowledgements => DuplicateAcks;
        public readonly ulong PersistDeadlineNanoseconds => PersistAt;
        public readonly ulong PersistProbes => PersistCount;
        public readonly TcpConnectionCloseState CloseState => CloseStateValue;
        public readonly bool WritesClosed => WriteShutdown != 0;
        public readonly bool FinOutstanding => FinPending != 0;
        public readonly ulong TimeWaitDeadlineNanoseconds => TimeWaitDeadline;
    }
}
