namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Original operation identity and directional offset. Transferred
    /// is the actual successful count,0 for zero/EOF, or-1 for failed/canceled
    /// I/O. Requested and Outcome remain explicit; no clock/HTTP meaning is added.</summary>
    public readonly record struct ServiceQuotaHostRange(int OperationId, long Offset, int Requested,
        int Transferred, string Outcome);

    /// <summary>Separately copied final failed/canceled write attempt. Its bytes
    /// are never appended to the known prefix. Actual transmission is unknown,
    /// including a zero-length attempt; the original transferred count is-1.</summary>
    public sealed record ServiceQuotaHostWriteAttempt(int OperationId, long Offset, int Requested,
        string Outcome, string AttemptedBase64)
    {
        public int Transferred => -1;
    }

    /// <summary>Copied known successful directional bytes and all original
    /// operation ranges, including zero/EOF/error events that add no known bytes.
    /// UnknownWrite exists only for a failed write direction, never a failed read.
    /// Base64 strings/lists are immutable; supplied values grant no I/O authority.</summary>
    public sealed record ServiceQuotaHostDirection(int KnownBytes, string KnownBase64,
        IReadOnlyList<ServiceQuotaHostRange> Operations, ServiceQuotaHostWriteAttempt? UnknownWrite);

    /// <summary>Both directions for the original session-local connection ID.
    /// No host endpoint, guest tuple, flow, sample or clock identity is inferred.</summary>
    public sealed record ServiceQuotaHostConnection(int ConnectionId, ServiceQuotaHostDirection Reads,
        ServiceQuotaHostDirection Writes);

    /// <summary>Copied byte inventory only. Unknown attempted bytes are counted
    /// separately from known bytes. Empty connections are retained in ID order;
    /// an empty quota-partial trace supplies no completion/execution witness.</summary>
    public sealed record ServiceQuotaHostPrefixes(int KnownReadBytes, int KnownWriteBytes,
        int UnknownAttemptedWriteBytes, IReadOnlyList<ServiceQuotaHostConnection> Connections);

    /// <summary>Synchronously borrow existing wire JSON, bounded by its strict
    /// inspector:32MiB UTF8,8192 connections,65536 operations,64KiB per operation,
    /// 4MiB total copied data. Invoke that inspector once, then inventory each
    /// successful read/write prefix and keep final attempted write data separate.
    /// Failed/canceled reads add no bytes; zero and EOF remain distinct ranges.
    /// Return copied/read-only values; malformed input throws through the
    /// original inspector. No new schema/CLI, HTTP/flow/sample/clock/process,
    /// packet/native/ownership/guest authority or FIN/RST logic is supplied.</summary>
    public static ServiceQuotaHostPrefixes InspectNativeAotServiceQuotaHostPrefixesForTests(
        ReadOnlySpan<byte> wireJson) => ReadServiceQuotaHostPrefixes(wireJson);

    private static ServiceQuotaHostPrefixes ReadServiceQuotaHostPrefixes(ReadOnlySpan<byte> wireJson)
        => ReadServiceQuotaHostPrefixes(NativeAotServiceQuotaPeer.InspectWireReport(wireJson));

    // Only callers which just ran the strict inspector may use this overload.
    private static ServiceQuotaHostPrefixes ReadServiceQuotaHostPrefixes(NativeAotServiceQuotaPeer.WireTrace trace)
    {
        var grouped=trace.Operations.GroupBy(op=>op.ConnectionId).ToDictionary(group=>group.Key,group=>group.ToArray());
        List<ServiceQuotaHostConnection> connections=[];
        int reads=0,writes=0,unknown=0;
        foreach(var connection in trace.Connections)
        {
            using MemoryStream read=new(),write=new();
            List<ServiceQuotaHostRange> readRanges=[],writeRanges=[];
            ServiceQuotaHostWriteAttempt? attempt=null;
            foreach(var op in grouped.GetValueOrDefault(connection.Id) ?? [])
            {
                bool writing=op.Direction=="write";
                var ranges=writing?writeRanges:readRanges;
                ranges.Add(new(op.Id,op.Offset,op.Requested,op.Transferred,op.Outcome));
                if(op.Outcome=="complete")
                {
                    byte[] data=Convert.FromBase64String(op.DataBase64);
                    (writing?write:read).Write(data);
                }
                else if(writing)
                {
                    // The original inspector admits only failed/canceled here,
                    // forbids later writes, and verifies the attempted byte count.
                    byte[] data=Convert.FromBase64String(op.DataBase64);
                    attempt=new(op.Id,op.Offset,op.Requested,op.Outcome,Convert.ToBase64String(data));
                    unknown+=data.Length;
                }
            }
            reads+=(int)read.Length;writes+=(int)write.Length;
            connections.Add(new(connection.Id,
                new((int)read.Length,Convert.ToBase64String(read.ToArray()),Array.AsReadOnly(readRanges.ToArray()),null),
                new((int)write.Length,Convert.ToBase64String(write.ToArray()),Array.AsReadOnly(writeRanges.ToArray()),attempt)));
        }
        Need((long)reads+writes+unknown==trace.RawBytes,"quota_host_prefix_accounting");
        return new(reads,writes,unknown,Array.AsReadOnly(connections.ToArray()));
    }
}
