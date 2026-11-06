namespace OS.Net;

public static unsafe partial class TcpReceiveWindow
{
    /// <summary>Initially zero, noncopyable metadata. Buffer and callback/code
    /// backing remain retained through explicit closed cleanup. Unread bytes are
    /// delivered only by Read; ACK backpressure never rolls back received data.
    /// Observations are not a socket or table authority.</summary>

    public struct Owner
    {
        internal ulong Self;
        internal TcpConnectionTable.Owner* Table;
        internal TcpConnectionTable.Handle Handle;
        internal TcpEstablishedStreamIpv4FramePlannerInfo Info;
        internal byte* Buffer;
        internal int CapacityValue, Offset, Unread;
        internal uint RemoteNext;
        internal void* Context;
        internal delegate*<void*, byte*, uint, byte> Send;
        internal byte Phase, AckPending, PeerClosed;
        public readonly bool Active => Phase == 1;
        public readonly bool RequiresCleanup => Phase == 2;
        public readonly bool Retired => Phase == 3;
        public readonly int UnreadBytes => Unread;
        public readonly int ReceiveCapacity => CapacityValue;
        public readonly ushort AvailableWindow => (ushort)(CapacityValue - Unread);
        public readonly uint AcknowledgementNumber => RemoteNext;
        public readonly bool HasPendingAck => AckPending != 0;
        public readonly bool EndOfStream => PeerClosed != 0 && Unread == 0;
    }
}
