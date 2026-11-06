namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow existing wire JSON, bounded by its strict
    /// inspector:32MiB UTF8,8192 connections,65536 operations,64KiB per operation,
    /// 4MiB total copied data. Invoke that inspector once, then inventory each
    /// successful read/write prefix and keep final attempted write data separate.
    /// Failed/canceled reads add no bytes; zero and EOF remain distinct ranges.
    /// Return copied/read-only values; malformed input throws through the
    /// original inspector. No new schema/CLI, HTTP/flow/sample/clock/process,
    /// packet/native/ownership/guest authority or FIN/RST logic is supplied.</summary>

    public static ServiceQuotaHostPrefixes InspectNativeAotServiceQuotaHostPrefixesForTests(ReadOnlySpan<byte> wireJson) => ReadServiceQuotaHostPrefixes(wireJson);
    private static ServiceQuotaHostPrefixes ReadServiceQuotaHostPrefixes(ReadOnlySpan<byte> wireJson) => ReadServiceQuotaHostPrefixes(NativeAotServiceQuotaPeer.InspectWireReport(wireJson));
    // Only callers which just ran the strict inspector may use this overload.
    private static ServiceQuotaHostPrefixes ReadServiceQuotaHostPrefixes(NativeAotServiceQuotaPeer.WireTrace trace)
    {
        var grouped = trace.Operations.GroupBy(op => op.ConnectionId).ToDictionary(group => group.Key, group => group.ToArray());
        List<ServiceQuotaHostConnection> connections = [];
        int reads = 0, writes = 0, unknown = 0;
        foreach (var connection in trace.Connections)
        {
            using MemoryStream read = new(), write = new();
            List<ServiceQuotaHostRange> readRanges = [], writeRanges = [];
            ServiceQuotaHostWriteAttempt? attempt = null;
            foreach (var op in grouped.GetValueOrDefault(connection.Id) ?? [])
            {
                bool writing = op.Direction == "write";
                var ranges = writing ? writeRanges : readRanges;
                ranges.Add(new(op.Id, op.Offset, op.Requested, op.Transferred, op.Outcome));
                if (op.Outcome == "complete")
                {
                    byte[] data = Convert.FromBase64String(op.DataBase64);
                    (writing ? write : read).Write(data);
                }
                else if (writing)
                {
                    // The original inspector admits only failed/canceled here,
                    // forbids later writes, and verifies the attempted byte count.
                    byte[] data = Convert.FromBase64String(op.DataBase64);
                    attempt = new(op.Id, op.Offset, op.Requested, op.Outcome, Convert.ToBase64String(data));
                    unknown += data.Length;
                }
            }

            reads += (int)read.Length;
            writes += (int)write.Length;
            connections.Add(new(connection.Id, new((int)read.Length, Convert.ToBase64String(read.ToArray()), Array.AsReadOnly(readRanges.ToArray()), null), new((int)write.Length, Convert.ToBase64String(write.ToArray()), Array.AsReadOnly(writeRanges.ToArray()), attempt)));
        }

        Need((long)reads + writes + unknown == trace.RawBytes, "quota_host_prefix_accounting");
        return new(reads, writes, unknown, Array.AsReadOnly(connections.ToArray()));
    }
}
