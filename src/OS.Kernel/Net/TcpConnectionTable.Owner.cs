namespace OS.Net;

public static unsafe partial class TcpConnectionTable
{
    /// <summary>Exclusive, noncopyable connection owner. One serialized kernel
    /// caller retains mapped state, entry and generation arrays through retirement.
    /// Count includes closed connections until explicit release. Observations
    /// grant no authority over packet buffers, timers, NICs or sockets.</summary>

    public struct Owner
    {
        internal ulong Self, Epoch;
        internal TcpConnectionTableEntry* Entries;
        internal ulong* Generations;
        internal ushort CapacityValue, Held;
        internal byte Phase;
        public readonly int Capacity => CapacityValue;
        public readonly int Count => Held;
        public readonly bool Retired => Phase == 2;
    }
}
