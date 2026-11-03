namespace OS.Net;

public static unsafe partial class TcpPassiveConnection
{
    /// <summary>Borrow a real monotonic nanosecond clock and frame-copying TX
    /// callback for the pending lifetime. Send returns1 only after accepting a
    /// complete copy,0 for capacity pressure, otherwise fatal failure. Retry time
    /// starts at observed copy completion. No frame pointer may escape Send.
    /// Context/code backing outlives pending cleanup.</summary>

    public struct Operations
    {
        public void* Context;
        public delegate*<void*, ulong> NowNanoseconds;
        public delegate*<void*, byte*, uint, byte> Send;
        public ulong LifetimeNanoseconds, RetryNanoseconds;
        public byte MaximumAttempts;
    }
}
