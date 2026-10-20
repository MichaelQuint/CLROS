namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private const int QuotaPacketMaximumExtent = 4 * 1024 * 1024;

    /// <summary>Copied bytes first seen at the original range's PCAP record.
    /// A range is an observed island, never invented hole contents. Original
    /// sec/usec values grant no host/guest-clock or execution authority.</summary>
    public sealed record ServiceQuotaPacketRange(ServiceWireByteRange First, string DataBase64);

    /// <summary>Logical extent includes holes; ContiguousBytes/KnownPrefixBase64
    /// stop at the first hole. FirstRanges retain every actual unique-byte island
    /// in original observation order. Their data is copied/read-only; an extent
    /// or byte count does not measure allocated component backing.</summary>
    public sealed record ServiceQuotaPacketDirection(int Extent, int ContiguousBytes, int KnownBytes,
        string KnownPrefixBase64, IReadOnlyList<ServiceQuotaPacketRange> FirstRanges);

    /// <summary>Observed TCP facts only. Missing FIN/RST is not an error or an
    /// invented orderly close. Flags do not prove fd/worker/kernel cleanup.</summary>
    public readonly record struct ServiceQuotaPacketFacts(int SynRecord, bool SynAck, bool ClientReady,
        bool ClientFin, bool ServerFin, bool ClientFinAcked, bool ServerFinAcked, bool ServerFinFirst,
        int ClientFinRecord, int ServerFinRecord, int ResetRecord);

    /// <summary>One selected guest-observed port; no host endpoint association
    /// or lifetime deduplication is made. Requests are client-to-guest direction.</summary>
    public sealed record ServiceQuotaPacketStream(uint RemotePort, ServiceQuotaPacketDirection Requests,
        ServiceQuotaPacketDirection Responses, ServiceQuotaPacketFacts Facts);

    /// <summary>Copied stream/range inventory; no HTTP, flow mapping, close,
    /// file/process, native, clock or guest acceptance verdict.</summary>
    public sealed record ServiceQuotaPacketStreams(int TcpRecords, int QuietSyns, int AggregateExtent,
        IReadOnlyList<ServiceQuotaPacketStream> Streams);

    /// <summary>Synchronously borrow original packet values and ordered distinct
    /// guest ports1..512. Reuse existing MAC/IP/TCP/ACK/retry and quiet-SYN rules;
    /// lifecycle explicitly selects the existing client-reset policy, defaultfalse.
    /// Quota-only directional AND aggregate request/response logical extents,
    /// including holes, are<=4MiB and checked before buffer growth. Original4MiB
    /// PCAP/65536 first-range limits remain. Return immutable copied prefixes,
    /// byte islands and transport facts without HTTP/close/FIN/RST completion
    /// requirements or new packet exemptions. Positive/default caps are unchanged.</summary>
    public static ServiceQuotaPacketStreams InspectNativeAotServiceQuotaPacketStreamsForTests(byte[] bytes,
        uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, bool lifecycle = false)
    {
        List<ServiceWireByteRange> ranges=[];
        var capture=ServiceWirePackets(bytes,anchor,arp,ports,concurrent:true,lifecycle:lifecycle,observe:range=>
        {
            Need(ranges.Count<MaximumServiceWireRanges,"service_wire_range_extent");ranges.Add(range);
        },quotaStreams:true);
        var grouped=ranges.GroupBy(range=>(range.RemotePort,range.ServerToClient))
            .ToDictionary(group=>group.Key,group=>group.ToArray());
        List<ServiceQuotaPacketStream> streams=[];
        int aggregate=0;
        foreach(var flow in capture.Flows)
        {
            ServiceQuotaPacketDirection Direction(bool outgoing)
            {
                byte[] data=outgoing?flow.Response:flow.Request;
                int extent=outgoing?flow.ResponseExtent:flow.RequestExtent,
                    contiguous=outgoing?flow.ServerSeen:flow.ClientSeen;
                var first=grouped.GetValueOrDefault((flow.Port,outgoing)) ?? [];
                var copied=first.Select(range=>new ServiceQuotaPacketRange(range,
                    Convert.ToBase64String(data.AsSpan(range.StreamOffset,range.ByteCount)))).ToArray();
                return new(extent,contiguous,first.Sum(range=>range.ByteCount),
                    Convert.ToBase64String(data.AsSpan(0,contiguous)),Array.AsReadOnly(copied));
            }
            aggregate+=flow.RequestExtent+flow.ResponseExtent;
            streams.Add(new(flow.Port,Direction(false),Direction(true),new(flow.SynRecord,flow.SynAck,flow.ClientReady,
                flow.ClientFin,flow.ServerFin,flow.ClientFinAcked,flow.ServerFinAcked,flow.ServerFinFirst,
                flow.ClientFinRecord,flow.ServerFinRecord,flow.ResetRecord)));
        }
        Need(aggregate<=QuotaPacketMaximumExtent,"service_quota_wire_aggregate");
        return new(capture.TcpRecords,capture.QuietSyns,aggregate,Array.AsReadOnly(streams.ToArray()));
    }
}
