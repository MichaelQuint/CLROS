namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private const int QuotaPacketMaximumExtent = 4 * 1024 * 1024;
    /// <summary>Synchronously borrow original packet values and ordered distinct
    /// guest ports1..512. Reuse existing MAC/IP/TCP/ACK/retry and quiet-SYN rules;
    /// lifecycle explicitly selects the existing client-reset policy, defaultfalse.
    /// Quota-only directional AND aggregate request/response logical extents,
    /// including holes, are<=4MiB and checked before buffer growth. Original4MiB
    /// PCAP/65536 first-range limits remain. Return immutable copied prefixes,
    /// byte islands and transport facts without HTTP/close/FIN/RST completion
    /// requirements or new packet exemptions. Positive/default caps are unchanged.</summary>

    public static ServiceQuotaPacketStreams InspectNativeAotServiceQuotaPacketStreamsForTests(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, bool lifecycle = false) => ReadQuotaPacketStreams(bytes, anchor, arp, ports, lifecycle);
    private static ServiceQuotaPacketStreams ReadQuotaPacketStreams(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, bool lifecycle, IReadOnlySet<int>? delegatedWebRecords = null)
    {
        List<ServiceWireByteRange> ranges = [];
        var capture = ServiceWirePackets(bytes, anchor, arp, ports, concurrent: true, lifecycle: lifecycle, observe: range =>
        {
            Need(ranges.Count < MaximumServiceWireRanges, "service_wire_range_extent");
            ranges.Add(range);
        }, quotaStreams: true, delegatedWebRecords: delegatedWebRecords);
        var grouped = ranges.GroupBy(range => (range.RemotePort, range.ServerToClient)).ToDictionary(group => group.Key, group => group.ToArray());
        List<ServiceQuotaPacketStream> streams = [];
        int aggregate = 0;
        foreach (var flow in capture.Flows)
        {
            ServiceQuotaPacketDirection Direction(bool outgoing)
            {
                byte[] data = outgoing ? flow.Response : flow.Request;
                int extent = outgoing ? flow.ResponseExtent : flow.RequestExtent, contiguous = outgoing ? flow.ServerSeen : flow.ClientSeen;
                var first = grouped.GetValueOrDefault((flow.Port, outgoing)) ?? [];
                var copied = first.Select(range => new ServiceQuotaPacketRange(range, Convert.ToBase64String(data.AsSpan(range.StreamOffset, range.ByteCount)))).ToArray();
                return new(extent, contiguous, first.Sum(range => range.ByteCount), Convert.ToBase64String(data.AsSpan(0, contiguous)), Array.AsReadOnly(copied));
            }

            aggregate += flow.RequestExtent + flow.ResponseExtent;
            streams.Add(new(flow.Port, Direction(false), Direction(true), new(flow.SynRecord, flow.SynAck, flow.ClientReady, flow.ClientFin, flow.ServerFin, flow.ClientFinAcked, flow.ServerFinAcked, flow.ServerFinFirst, flow.ClientFinRecord, flow.ServerFinRecord, flow.ResetRecord)));
        }

        Need(aggregate <= QuotaPacketMaximumExtent, "service_quota_wire_aggregate");
        return new(capture.TcpRecords, capture.QuietSyns, aggregate, Array.AsReadOnly(streams.ToArray()));
    }
}
