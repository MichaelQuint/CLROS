namespace OS.Tooling;

public static partial class NativeDiskReadObservations
{
    /// <summary>
    /// Borrow a bounded original diagnostic transcript and unchanged image.
    /// Select every diagnostic row in its raw order between independent native
    /// BEGIN/END counters. Join actual binding/listen readiness, record and copy
    /// totals, cancellation, clocks, and the final file/result after the timing
    /// aggregates. Trace requires exact bytes; summary supplies CRC evidence only.
    /// The caller retains image/source/native/process authority and separately
    /// validates the existing timing aggregates and HTTP responses. This does not
    /// qualify full execution or normal cleanup. Malformed/incomplete/mixed or
    /// divergent evidence throws ToolFailure. Inputs are neither mutated nor kept.
    /// </summary>
    public static DiagnosticInspection InspectDiagnosticTranscript(string text, ReadOnlySpan<byte> rawImage,
        bool payloadIncluded, ulong assetInode, ulong assetBytes)
    {
        Need(text is not null && text.Length <= 32 * 1024 * 1024 && assetInode != 0 && assetBytes >= 65536,
            "diagnostic_transcript_input");
        string[] lines = text!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int boundIndex = UniqueDiagnosticRow(lines, "M16_023_BOUND ");
        int readyIndex = UniqueDiagnosticRow(lines, "M10_018_DISK_READY ");
        int beginIndex = UniqueDiagnosticRow(lines, "CLROS_DISK_IO_BEGIN_V1 ");
        int endIndex = UniqueDiagnosticRow(lines, "CLROS_DISK_IO_END_V1 ");
        Need(boundIndex < readyIndex && beginIndex == readyIndex + 1 && endIndex > beginIndex,
            "diagnostic_transcript_order");
        var binding = ReadDiagnosticBinding(lines[boundIndex]);
        var ready = DiagnosticValues(lines[readyIndex], "M10_018_DISK_READY", "submitted", "completed", "copied", "clock");
        string[] fields = ["payload", "owner", "epoch", "submitted", "completed", "copied", "cancelled", "records", "copiedRecords", "clock"];
        var begin = DiagnosticValues(lines[beginIndex], "CLROS_DISK_IO_BEGIN_V1", fields);
        var end = DiagnosticValues(lines[endIndex], "CLROS_DISK_IO_END_V1", fields);
        ulong payload = payloadIncluded ? 1UL : 0UL;
        Need(binding.Capacity == (ulong)rawImage.Length / 512 &&
            begin["payload"] == payload && end["payload"] == payload &&
            begin["owner"] == binding.Owner && end["owner"] == binding.Owner &&
            begin["epoch"] == binding.Epoch && end["epoch"] == binding.Epoch &&
            begin["submitted"] == ready["submitted"] && begin["completed"] == ready["completed"] &&
            begin["copied"] == ready["copied"] && begin["clock"] == ready["clock"] &&
            begin["submitted"] == begin["completed"] && begin["copied"] <= begin["completed"] &&
            begin["completed"] >= binding.Completed && begin["copied"] >= binding.Copied &&
            begin["copied"] - binding.Copied <= begin["completed"] - binding.Completed &&
            begin["records"] == 0 && begin["copiedRecords"] == 0 &&
            begin["clock"] >= binding.Clock && end["clock"] >= begin["clock"] &&
            end["cancelled"] >= begin["cancelled"] && end["copied"] >= begin["copied"] &&
            end["copiedRecords"] == end["copied"] - begin["copied"], "diagnostic_transcript_boundary");

        List<string> rows = [];
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            Need(!line.Contains("M16_023_IO", StringComparison.Ordinal), "diagnostic_transcript_mixed");
            bool diagnostic = line.Contains("CLROS_DISK_IO", StringComparison.Ordinal) ||
                index == lines.Length - 1 && line.Length != 0 && "CLROS_DISK_IO".StartsWith(line, StringComparison.Ordinal);
            if (!diagnostic)
                continue;
            Need(index < lines.Length - 1 && line.Length <= MaximumRowChars, "diagnostic_transcript_partial");
            if (index == beginIndex || index == endIndex)
                continue;
            Need(index > beginIndex && index < endIndex && line.StartsWith("CLROS_DISK_IO_V1 ", StringComparison.Ordinal) &&
                rows.Count < MaximumRows, "diagnostic_transcript_scope");
            rows.Add(line);
        }
        Need(end["records"] == (ulong)rows.Count && end["copiedRecords"] > 0,
            "diagnostic_transcript_record_count");
        var boundary = new Boundary(binding.Owner, binding.Epoch, begin["completed"], begin["copied"],
            end["submitted"], end["completed"], end["copied"]);
        var result = InspectDiagnostic(rows, rawImage, boundary, payloadIncluded);
        Need(result.Cancelled == end["cancelled"] - begin["cancelled"], "diagnostic_transcript_cancellation");
        foreach (string row in rows)
        {
            int start = row.IndexOf(" clock=", StringComparison.Ordinal);
            Need(start >= 0, "diagnostic_transcript_clock");
            ReadOnlySpan<char> suffix = row.AsSpan(start);
            ulong clock = Number(ref suffix, " clock=");
            Need(clock >= begin["clock"] && clock <= end["clock"], "diagnostic_transcript_clock");
        }
        InspectDiagnosticFinalRead(lines, endIndex, assetInode, assetBytes);
        return result;
    }

    private static int UniqueDiagnosticRow(string[] lines, string prefix)
    {
        int selected = -1;
        for (int index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith(prefix, StringComparison.Ordinal))
                continue;
            Need(selected == -1 && index < lines.Length - 1 && lines[index].Length <= MaximumRowChars,
                "diagnostic_transcript_marker");
            selected = index;
        }
        Need(selected >= 0, "diagnostic_transcript_marker");
        return selected;
    }

    private static Dictionary<string, ulong> DiagnosticValues(string row, string marker, params string[] fields)
    {
        Need(row.StartsWith(marker, StringComparison.Ordinal) && row.Length <= MaximumRowChars,
            "diagnostic_transcript_shape");
        ReadOnlySpan<char> remaining = row.AsSpan(marker.Length);
        Dictionary<string, ulong> result = new(StringComparer.Ordinal);
        foreach (string field in fields)
            result.Add(field, Number(ref remaining, " " + field + "="));
        Need(remaining.IsEmpty, "diagnostic_transcript_suffix");
        return result;
    }

    private static (ulong Owner, ulong Epoch, ulong Capacity, ulong Completed, ulong Copied, ulong Clock) ReadDiagnosticBinding(string row)
    {
        ReadOnlySpan<char> remaining = row.AsSpan();
        ulong owner = Number(ref remaining, "M16_023_BOUND owner=");
        ulong epoch = Number(ref remaining, " epoch=");
        _ = Number(ref remaining, " arena=");
        _ = Number(ref remaining, " pages=");
        _ = Number(ref remaining, " bar=");
        ulong capacity = Number(ref remaining, " capacity=");
        ulong completed = Number(ref remaining, " bootstrapCompleted=");
        ulong copied = Number(ref remaining, " bootstrapCopied=");
        ulong clock = Number(ref remaining, " clock=");
        Need(owner != 0 && epoch != 0 && copied <= completed && remaining.SequenceEqual(
            " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained"),
            "diagnostic_transcript_binding");
        return (owner, epoch, capacity, completed, copied, clock);
    }

    private static void InspectDiagnosticFinalRead(string[] lines, int endIndex, ulong assetInode, ulong assetBytes)
    {
        int position = endIndex + 1;
        foreach (string marker in new[] { NativeAotDiskTimingPeer.Marker, NativeAotDiskTimingPeer.DispatchMarker,
            NativeAotDiskTimingPeer.DriverMarker, NativeAotDiskTimingPeer.VerificationMarker })
        {
            Need(position < lines.Length - 1 && lines[position].StartsWith(marker, StringComparison.Ordinal),
                "diagnostic_transcript_aggregates");
            position++;
        }
        Need(position + 1 < lines.Length - 1, "diagnostic_transcript_final_read");
        var file = DiagnosticValues(lines[position], "M10_018_FILE", "identity", "ordinal", "nr", "fd", "generation",
            "kind", "view", "inode", "bytes", "position", "afterPosition");
        var completion = DiagnosticValues(lines[position + 1], "M16_025_COMPLETE", "identity", "sequence", "request",
            "outputBytes", "completion", "valueBits", "clock");
        Need(file["identity"] != 0 && file["ordinal"] != 0 && file["nr"] is 0 or 17 &&
            file["fd"] < OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices.MaximumDescriptors &&
            file["generation"] is > 0 and <= uint.MaxValue && file["kind"] == 5 && file["view"] == uint.MaxValue &&
            file["inode"] == assetInode && file["bytes"] == assetBytes &&
            completion["identity"] == file["identity"] && completion["sequence"] != 0 && completion["request"] != 0 &&
            completion["completion"] == 1 && completion["outputBytes"] > 0 &&
            completion["outputBytes"] <= OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices.DiskCompletionBytes &&
            completion["valueBits"] == completion["outputBytes"] &&
            (file["nr"] == 17 ? file["afterPosition"] == file["position"] :
                file["position"] <= assetBytes && completion["outputBytes"] <= assetBytes - file["position"] &&
                file["afterPosition"] == file["position"] + completion["outputBytes"]), "diagnostic_transcript_final_read");
    }
}
