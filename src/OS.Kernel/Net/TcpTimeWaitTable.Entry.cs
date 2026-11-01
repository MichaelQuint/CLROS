namespace OS.Net;
public static unsafe partial class TcpTimeWaitTable
{
    /// <summary>Caller-owned compact backing, initially all zero. Only this table
    /// mutates it. Expiry/discard scrub every byte, including padding/generation.</summary>

    public struct Entry
    {
        internal Record Value;
        internal ulong Generation;
    }
}
