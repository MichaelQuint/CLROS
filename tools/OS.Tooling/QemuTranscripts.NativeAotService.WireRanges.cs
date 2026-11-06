namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private const int MaximumServiceWireRanges = 65_536;
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

    public static IReadOnlyList<ServiceWireByteRange> InspectNativeAotServiceWireRangesForTests(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, bool lifecycle = false)
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
