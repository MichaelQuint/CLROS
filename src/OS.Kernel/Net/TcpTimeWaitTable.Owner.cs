namespace OS.Net;

public static unsafe partial class TcpTimeWaitTable
{
    /// <summary>Noncopyable owner of1..64 disjoint zeroed entries. Issued handle
    /// generations never wrap or reset during this epoch, including on discard.</summary>

    public struct Owner
    {
        internal ulong Self, Epoch, Issued, LastNow;
        internal Entry* Entries;
        internal uint CapacityValue, Held;
        public readonly int Count => (int)Held;
        public readonly uint Capacity => CapacityValue;
    }
}
