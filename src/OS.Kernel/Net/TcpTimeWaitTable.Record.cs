namespace OS.Net;
public static unsafe partial class TcpTimeWaitTable
{
    /// <summary>Immutable tuple/sequences and exact original/current deadlines.
    /// Restart preserves the original deadline for evidence and changes only the
    /// current deadline. Zero/wrapped TCP sequence numbers are ordinary values.</summary>

    public readonly struct Record
    {
        internal Record(TcpConnectionKey key, uint local, uint remote, ulong original, ulong deadline, ulong duration)
        {
            Key = key;
            LocalNextSequenceNumber = local;
            RemoteNextSequenceNumber = remote;
            OriginalDeadlineNanoseconds = original;
            DeadlineNanoseconds = deadline;
            DurationNanoseconds = duration;
        }

        public TcpConnectionKey Key { get; }
        public uint LocalNextSequenceNumber { get; }
        public uint RemoteNextSequenceNumber { get; }
        public ulong OriginalDeadlineNanoseconds { get; }
        public ulong DeadlineNanoseconds { get; }
        public ulong DurationNanoseconds { get; }
    }
}
