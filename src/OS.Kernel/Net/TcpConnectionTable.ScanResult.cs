namespace OS.Net;
public static unsafe partial class TcpConnectionTable
{
    private readonly struct ScanResult
    {
        public ScanResult(TcpConnectionTableStatus status, int count, int firstReusableEntryIndex, int matchingEntryIndex)
        {
            Status = status;
            Count = count;
            FirstReusableEntryIndex = firstReusableEntryIndex;
            MatchingEntryIndex = matchingEntryIndex;
        }

        public TcpConnectionTableStatus Status { get; }
        public int Count { get; }
        public int FirstReusableEntryIndex { get; }
        public int MatchingEntryIndex { get; }
    }
}
