namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>First appearance of a contiguous set of unique TCP stream bytes.
    /// StreamOffset is relative to the selected direction's first post-SYN byte;
    /// Record is the one-based original PCAP record. Seconds/Microseconds retain
    /// the original classic-PCAP units. RemotePort is the guest-observed tuple,
    /// not the peer's host socket port. ServerToClient names the guest response
    /// direction. Values carry no host-clock, exchange or execution authority.</summary>

    public readonly record struct ServiceWireByteRange(uint RemotePort, bool ServerToClient, int StreamOffset, int ByteCount, int Record, uint Seconds, uint Microseconds)
    {
        public ulong TimestampMicroseconds => (ulong)Seconds * 1_000_000 + Microseconds;
    }
}
