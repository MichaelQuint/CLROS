namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously inspect borrowed framed canonical call values.
    /// Invalid arena sequences or impossible backing throw; return values only.
    /// Original native/source review and complete replay remain independent.</summary>

    public static ServiceGcBackingInspection InspectNativeAotServiceGcBackingForTests(ReadOnlySpan<byte> raw)
    {
        var framed = NativeAotThreadingOutput.ReadService(raw);
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_gc_backing_records");
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is > 0 and <= 1_000_000 && rows.All(r => r.Length is > 0 and <= 4096), "service_gc_backing_records");
        return ReadServiceGcBacking(rows, ValidateLinuxServiceCallOrdinals(rows), ReadLinuxServiceCallReturns(rows).Select(r => r.RecordIndex).ToHashSet()).Summary;
    }

    private static (ServiceGcBackingInspection Summary, Dictionary<int, ulong> At) ReadServiceGcBacking(string[] rows, string[] calls, HashSet<int> returned)
    {
        var ledger = new ServiceGcBacking();
        var at = new Dictionary<int, ulong>();
        var ordinals = ReadLinuxServiceCallReturns(rows).ToDictionary(r => r.RecordIndex, r => r.Ordinal);
        for (int i = 0; i < rows.Length; i++)
            if (returned.Contains(i))
            {
                var call = ReadRecord(calls[i], "M16_008_CALL", WebCallFields);
                ledger.Observe(call, ordinals[i], i);
                if (call["nr"] is 9 or 10 or 11 or 12 or 25 or 28)
                    at.Add(i, ledger.BackedBytes);
            }

        return (ledger.Inspection, at);
    }
}
