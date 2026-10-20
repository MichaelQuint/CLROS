using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One clipped stderr marker span in a genuine original fd2 write
    /// and its following successful write/writev CALL. Offsets/counts are bytes
    /// in reassembled fd2; row indices address original normalized serial rows.</summary>
    public readonly record struct ServiceQuotaProgressSlice(int WriteRecordIndex, int CallRecordIndex,
        ulong Identity, ulong Ordinal, int ByteOffset, int ByteCount);
    /// <summary>Copied complete application marker and every segmented write
    /// needed to publish it. Text is progress data, never native policy authority.</summary>
    public sealed record ServiceQuotaProgressMarker(string Name, int Index, int ByteStart, int ByteEnd,
        IReadOnlyList<ServiceQuotaProgressSlice> Writes);
    /// <summary>Selected HTTP sample bounds joined to original socket CALL rows.
    /// A poll's request may precede the retained mmap; its response must follow.</summary>
    public readonly record struct ServiceQuotaOverlapSample(string Name, int ExchangeId, int ConnectionId,
        int RequestFirstCall, int RequestLastCall, int ResponseFirstCall, int ResponseLastCall);
    /// <summary>Copied same-retention-window values only. ForcedGcMarkers are
    /// completed app collection-delta observations, not GC scheduler/root proof.
    /// No clocks, pending sockets, process/file/native or guest authority follow.</summary>
    public sealed record ServiceQuotaOverlapInspection(ServiceQuotaRetainedMapping FirstRetained, int DenialCall,
        ServiceQuotaProgressMarker Begin, IReadOnlyList<ServiceQuotaProgressMarker> ForcedGcMarkers,
        IReadOnlyList<ServiceQuotaOverlapSample> Samples);

    /// <summary>Borrow original bounded framed bytes beside already strict
    /// Memory/HostHttp/Temporal values; inspect the supplied strict peer JSON.
    /// Rejoin retained mmap positions and every selected socket slice to actual
    /// original CALLs. Map fd2 markers across arbitrary segmentation to genuine
    /// write/writev CALLs. Require held cancel/echo prefixes before pressure;
    /// selected quota/worker/health/lifecycle responses and forced-GC markers
    /// must lie after the first still-retained allocation and before denial.
    /// RETAINED publication need not precede quota.retained's response. This
    /// value seam does not replace complete replay, owned pending peer/packet
    /// or native joins, prove directed-signal readiness, or grant acceptance.</summary>
    public static ServiceQuotaOverlapInspection InspectNativeAotServiceQuotaOverlapForTests(ReadOnlySpan<byte> raw,
        ServiceQuotaMemoryInspection memory, ServiceQuotaHostHttpInspection host, ServiceQuotaTemporalInspection temporal,
        ReadOnlySpan<byte> peerJson) => ReadServiceQuotaOverlap(NativeAotThreadingOutput.ReadService(raw), memory,
            host, temporal, NativeAotServiceQuotaPeer.InspectReport(peerJson));

    private static ServiceQuotaOverlapInspection ReadServiceQuotaOverlap(NativeAotThreadingOutput.Capture framed,
        ServiceQuotaMemoryInspection memory, ServiceQuotaHostHttpInspection host, ServiceQuotaTemporalInspection temporal,
        NativeAotServiceQuotaPeer.Observation peer)
    {
        if (framed is null || host is null || host.Prefixes is null || temporal is null || peer is null) throw new ToolFailure("quota_overlap_input");
        var output = InspectNativeAotServiceQuotaOutputForTests(framed.Stdout, framed.Stderr);
        var retained = QuotaPairCopy(memory.RetainedCalls, 63);
        var messages = QuotaPairCopy(host.Messages, 8192); var selections = QuotaPairCopy(host.Samples, 18);
        var connections = QuotaPairCopy(host.Prefixes.Connections, 8192);
        var intervals = QuotaPairCopy(temporal.Intervals, 16_384); var samples = QuotaPairCopy(peer.Samples, 18);
        Need(retained.Length > 0 && (ulong)retained.Length == output.CompletedChunks && memory.RetainedMappings == output.CompletedChunks &&
            memory.RetainedBytes == output.RetainedBytes && selections.Length == 18 && samples.Length == 18,
            "quota_overlap_projection");
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "quota_overlap_rows");
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is > 0 and <= 1_000_000, "quota_overlap_rows");
        var normalized = ValidateLinuxServiceCallOrdinals(rows);
        var returns = ReadLinuxServiceCallReturns(rows).ToDictionary(r => r.RecordIndex);
        var calls = returns.Keys.ToDictionary(at => at, at => ReadRecord(normalized[at], "M16_008_CALL", WebCallFields));
        int denial = memory.Denial.RecordIndex + 2;
        Need(returns.ContainsKey(denial) && denial == returns.Keys.Max() && calls[denial]["nr"] == 9 &&
            calls[denial]["a0"] == 0 && calls[denial]["a1"] == 1048576 && calls[denial]["a2"] == 3 && calls[denial]["a3"] == 0x22 &&
            (uint)calls[denial]["a4"] == uint.MaxValue && calls[denial]["a5"] == 0 && calls[denial]["action"] == 1 && calls[denial]["status"] == 1 &&
            calls[denial]["valueBits"] == unchecked((ulong)-12L) && returns[denial].Identity == memory.Denial.Identity &&
            returns[denial].Ordinal == memory.Denial.Ordinal, "quota_overlap_denial");
        for (int i = 0; i < retained.Length; i++)
        {
            var held = retained[i]; bool exists = calls.TryGetValue(held.CallRecordIndex, out var call);
            if (!exists || call is null) throw new ToolFailure("quota_overlap_retained");
            Need(held.Index == i && held.Bytes == 1048576 && held.CallRecordIndex < denial &&
                (i == 0 || held.CallRecordIndex > retained[i-1].CallRecordIndex) && call["nr"] == 9 && call["a0"] == 0 &&
                call["a1"] == held.Bytes && call["a2"] == 3 && call["a3"] == 0x22 && (uint)call["a4"] == uint.MaxValue &&
                call["a5"] == 0 && call["action"] == 1 && call["status"] == 1 && call["valueBits"] == held.Address &&
                held.Address is >= 0x100000000 and < 0x1000000000 && held.Address % 4096 == 0 &&
                returns[held.CallRecordIndex].Identity == held.Identity && returns[held.CallRecordIndex].Ordinal == held.Ordinal,
                "quota_overlap_retained");
        }
        List<(int At, int Call, int Start, int End, ulong Identity, ulong Ordinal)> writes = [];
        int written = 0, write = 0;
        for (int at = 0; at < rows.Length; at++) if (rows[at].StartsWith("M16_005_WRITE", StringComparison.Ordinal))
        {
            Need(write < framed.WriteLengths.Count && framed.WriteChannels[write] == 2, "quota_overlap_write");
            int bytes = framed.WriteLengths[write++];
            Need(rows[at] == "M16_005_WRITE fd=2 bytes=" + bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " data=" &&
                returns.ContainsKey(at+1), "quota_overlap_write_call");
            var call = calls[at+1]; var returned = returns[at+1];
            Need(call["nr"] is 1 or 20 && call["a0"] == 2 && call["valueBits"] == (ulong)bytes && call["action"] == 1 &&
                call["status"] == 1 && returned.ValueBits == (ulong)bytes && returned.Identity == call["identity"],
                "quota_overlap_write_call");
            Need(call["nr"] == 1 ? call["a2"] is > 0 and <= 65536 && (ulong)bytes <= call["a2"] :
                call["a2"] is > 0 and <= OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices.MaximumVectors,
                "quota_overlap_write_call");
            writes.Add((at,at+1,written,written+bytes,returned.Identity,returned.Ordinal)); written += bytes;
        }
        Need(write == framed.WriteLengths.Count && written == Encoding.UTF8.GetByteCount(framed.Stderr), "quota_overlap_write_count");
        ServiceQuotaProgressMarker Marker(string name, int index)
        {
            string prefix = name + (index < 0 ? " " : " index=" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            int start = framed.Stderr.IndexOf(prefix, StringComparison.Ordinal);
            Need(start >= 0 && (start == 0 || framed.Stderr[start-1] == '\n') &&
                framed.Stderr.IndexOf(prefix,start+prefix.Length,StringComparison.Ordinal) < 0, "quota_overlap_marker");
            int end = framed.Stderr.IndexOf('\n',start); Need(end >= start, "quota_overlap_marker"); end++;
            int first = Encoding.UTF8.GetByteCount(framed.Stderr.AsSpan(0,start)), last = Encoding.UTF8.GetByteCount(framed.Stderr.AsSpan(0,end));
            var sliced = writes.Where(w => w.Start < last && w.End > first).Select(w => new ServiceQuotaProgressSlice(w.At,w.Call,w.Identity,w.Ordinal,
                Math.Max(first,w.Start),Math.Min(last,w.End)-Math.Max(first,w.Start))).ToArray();
            Need(sliced.Length > 0 && sliced.Sum(w => w.ByteCount) == last-first, "quota_overlap_marker");
            return new(name,index,first,last,Array.AsReadOnly(sliced));
        }
        var begin = Marker("CLROS_SERVICE_QUOTA_BEGIN",-1);
        Need(begin.Writes[^1].CallRecordIndex < retained[0].CallRecordIndex, "quota_overlap_begin");
        List<ServiceQuotaProgressMarker> gc = [];
        for (int i = 0; i < Math.Min(retained.Length,4); i++)
        {
            var marker = Marker("CLROS_SERVICE_QUOTA_RETAINED",i);
            Need(retained[i].CallRecordIndex < marker.Writes[0].CallRecordIndex && marker.Writes[^1].CallRecordIndex < denial,
                "quota_overlap_gc_window"); gc.Add(marker);
        }
        // Temporal already validates complete directional coverage/FIFO. Bind
        // every borrowed slice back to its exact native row before using order.
        Dictionary<(int Exchange,int Connection,string Kind), (int First,int Last,int Start,int End)> bounds = [];
        int sliceCount = 0;
        foreach (var interval in intervals)
        {
            if (interval is null) throw new ToolFailure("quota_overlap_input");
            var slices = QuotaPairCopy(interval.Slices, 1_000_000); ulong cursor = (ulong)interval.Start;
            Need(interval.Start >= 0 && interval.End > interval.Start && slices.Length > 0, "quota_overlap_interval");
            int previous = -1;
            foreach (var slice in slices)
            {
                var io = slice.Original;
                Need(++sliceCount <= 2_000_000 && io.CallRecordIndex > previous && io.RecordIndex >= 0 && io.RecordIndex < rows.Length && returns.ContainsKey(io.CallRecordIndex) &&
                    QuotaSocketRow(rows[io.RecordIndex],io.RecordIndex) == io && !io.Accept && !io.Peek && io.Response == interval.Response &&
                    io.Connection == interval.Connection && slice.ByteCount > 0 && slice.StreamOffset == cursor &&
                    slice.StreamOffset >= io.Offset && slice.StreamOffset-io.Offset <= io.Bytes &&
                    slice.ByteCount <= io.Bytes-(slice.StreamOffset-io.Offset), "quota_overlap_slice");
                QuotaSocketCallBinding(io,calls[io.CallRecordIndex],returns[io.CallRecordIndex]); cursor += slice.ByteCount;
                previous = io.CallRecordIndex;
            }
            Need(cursor == (ulong)interval.End && bounds.TryAdd((interval.ExchangeId,interval.ConnectionId,interval.Kind),
                (slices[0].Original.CallRecordIndex,slices[^1].Original.CallRecordIndex,interval.Start,interval.End)), "quota_overlap_interval");
        }
        ServiceQuotaOverlapSample Sample(string name)
        {
            var selected = selections.Where(s => s.Name == name).ToArray(); var copied = samples.Where(s => s.Name == name).ToArray();
            Need(selected.Length == 1 && copied.Length == 1 && selected[0].MessageIndex >= 0 && selected[0].MessageIndex < messages.Length,
                "quota_overlap_sample");
            var message = messages[selected[0].MessageIndex];
            if (message is null || message.Response is null) throw new ToolFailure("quota_overlap_sample");
            string kind = selected[0].HeadersOnly ? "cancel-header" : "complete-response";
            bool requestFound = bounds.TryGetValue((message.ExchangeId,message.ConnectionId,"complete-request"),out var request),
                responseFound = bounds.TryGetValue((message.ExchangeId,message.ConnectionId,kind),out var response);
            Need(requestFound && responseFound && message.Request.State == "complete" &&
                request.Start == message.Request.Start && request.End == message.Request.End && response.Start == message.Response.Start &&
                response.End == (selected[0].HeadersOnly ? message.Response.HeaderEnd : message.Response.End) &&
                (selected[0].HeadersOnly || message.Response.State == "complete") &&
                (selected[0].HeadersOnly || message.Response.BodyBase64 == Convert.ToBase64String(Encoding.ASCII.GetBytes(copied[0].Body))),
                "quota_overlap_sample_binding");
            if (selected[0].HeadersOnly)
            {
                var matches = connections.Where(c => c.ConnectionId == message.ConnectionId).ToArray();
                Need(matches.Length == 1 && matches[0].Reads.KnownBytes == message.Response.HeaderEnd &&
                    matches[0].Reads.KnownBase64 == Convert.ToBase64String(Encoding.ASCII.GetBytes(copied[0].Body)), "quota_overlap_sample_binding");
            }
            return new(name,message.ExchangeId,message.ConnectionId,request.First,request.Last,response.First,response.Last);
        }
        var cancel = Sample("cancel.headers"); var heldSample = Sample("lifecycle.held");
        var echoes = intervals.Where(i => i.Kind == "echo-prefix").ToArray();
        var pressure = intervals.Where(i => i.Kind == "pressure-request").ToArray();
        Need(echoes.Length == 1 && pressure.Length == 1 && cancel.ResponseLastCall < begin.Writes[0].CallRecordIndex &&
            heldSample.ResponseLastCall < begin.Writes[0].CallRecordIndex &&
            bounds[(echoes[0].ExchangeId,echoes[0].ConnectionId,"echo-prefix")].Last < begin.Writes[0].CallRecordIndex &&
            bounds[(pressure[0].ExchangeId,pressure[0].ConnectionId,"pressure-request")].Last < begin.Writes[0].CallRecordIndex,
            "quota_overlap_held_before_pressure");
        List<ServiceQuotaOverlapSample> progress = [];
        foreach (string name in new[] { "quota.retained", "worker.after_retained", "health.overlap", "worker.progress",
            "lifecycle.overlap", "quota.overlap", "quota.last" })
        {
            var sample = Sample(name);
            Need(sample.ResponseFirstCall > retained[0].CallRecordIndex && sample.ResponseLastCall < denial,
                "quota_overlap_response_window");
            if (progress.Count > 0)
            {
                var prior = progress[^1];
                Need(name == "quota.last" && sample.ExchangeId == prior.ExchangeId && sample.ConnectionId == prior.ConnectionId ||
                    sample.RequestFirstCall > prior.ResponseLastCall, "quota_overlap_progress_order");
            }
            progress.Add(sample);
        }
        Need(peer.RetainedBytes <= memory.RetainedBytes && peer.GcCollections <= output.ForcedCollections, "quota_overlap_accounting");
        return new(retained[0],denial,begin,Array.AsReadOnly(gc.ToArray()),Array.AsReadOnly(progress.ToArray()));
    }
}
