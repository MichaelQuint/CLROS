namespace OS.Net;

public static partial class TcpListenerBacklog
{
    private readonly struct ScanResult
    {
        public ScanResult(TcpListenerBacklogStatus status, int count, int firstFreeEntryIndex, int matchingEntryIndex, int oldestEntryIndex, ulong maximumEnqueueOrder)
        {
            Status = status;
            Count = count;
            FirstFreeEntryIndex = firstFreeEntryIndex;
            MatchingEntryIndex = matchingEntryIndex;
            OldestEntryIndex = oldestEntryIndex;
            MaximumEnqueueOrder = maximumEnqueueOrder;
        }

        public TcpListenerBacklogStatus Status
        {
            get;
        }
        public int Count
        {
            get;
        }
        public int FirstFreeEntryIndex
        {
            get;
        }
        public int MatchingEntryIndex
        {
            get;
        }
        public int OldestEntryIndex
        {
            get;
        }
        public ulong MaximumEnqueueOrder
        {
            get;
        }
    }
}
