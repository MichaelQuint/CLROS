namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private const int MaximumServiceWireRanges = 65_536;

    /// <summary>First appearance of a contiguous set of unique TCP stream bytes.
    /// StreamOffset is relative to the selected direction's first post-SYN byte;
    /// Record is the one-based original PCAP record. Seconds/Microseconds retain
    /// the original classic-PCAP units. RemotePort is the guest-observed tuple,
    /// not the peer's host socket port. ServerToClient names the guest response
    /// direction. Values carry no host-clock, exchange or execution authority.</summary>
    public readonly record struct ServiceWireByteRange(uint RemotePort, bool ServerToClient, int StreamOffset,
        int ByteCount, int Record, uint Seconds, uint Microseconds)
    {
        public ulong TimestampMicroseconds => (ulong)Seconds * 1_000_000 + Microseconds;
    }

    /// <summary>Synchronously borrow a bounded original PCAP, parent and ordered
    /// remote ports, and return immutable copied first-appearance ranges through
    /// the existing strict TCP parser. Exact retransmissions add no observations;
    /// overlapping new bytes retain their own first original record/time even
    /// when sequence order differs from capture order. Reject malformed framing,
    /// backward PCAP time, invalid TCP/ACK/tuple bytes or more than65,536 ranges.
    /// The original4MiB PCAP and per-flow stream bounds still apply. Lifecycle
    /// selects the existing larger streams/client-reset policy without changing
    /// positive defaults. No input is modified or retained. Ranges describe only
    /// observed bytes: holes, unclosed flows and unfinished HTTP are not promoted
    /// to complete exchanges, negative teardown or file/process/guest authority.
    /// Peer-clock correlation and packet/peer/syscall temporal joins are separate.</summary>
    public static IReadOnlyList<ServiceWireByteRange> InspectNativeAotServiceWireRangesForTests(byte[] bytes, uint anchor,
        QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, bool lifecycle = false)
    {
        List<ServiceWireByteRange> ranges = [];
        _ = ServiceWirePackets(bytes, anchor, arp, ports, concurrent: true, lifecycle: lifecycle, observe: range =>
        {
            Need(ranges.Count < MaximumServiceWireRanges, "service_wire_range_extent");
            ranges.Add(range);
        });
        return Array.AsReadOnly(ranges.ToArray());
    }
}
