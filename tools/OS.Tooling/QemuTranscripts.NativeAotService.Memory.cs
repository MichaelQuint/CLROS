using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied post-dispatch backing observations. Ordinary peak excludes
    /// deliberate pressure and includes its starting backing. Conservative bounds
    /// count that entire peak as non-GC, so can overestimate demand. Native failures
    /// describe the two probe-shaped mmap requests. A physical shortfall does
    /// not identify the sole failing guard; other causes can coexist.
    /// Values confer no allocation, native-review or guest execution authority.</summary>
    public readonly record struct ServiceMemoryInspection(int OperationSnapshots, ulong OrdinaryPeakBackedBytes,
        ulong MaximumRegionCount, int NativePhysicalShortfalls, int NativeUnclassifiedFailures,
        ulong ConservativePoolBytes, ulong ConservativeEntitlementBytes, bool ConservativeHeadroomFits);

    /// <summary>Synchronously inspect bounded borrowed framed service bytes and
    /// GC/native values. Invalid, missing or inconsistent observations throw.
    /// Return copied values only; full syscall/lifetime/native/guest validation
    /// belongs to the parent replay. V1 validates legacy failure snapshots only.</summary>
    public static ServiceMemoryInspection? InspectNativeAotServiceMemoryForTests(ReadOnlySpan<byte> raw,
        ulong entitlementBytes, bool lifecycle = false)
    {
        var framed = NativeAotThreadingOutput.ReadService(raw);
        var gc = ReadServiceGc(framed, entitlementBytes, lifecycle, out _, out _);
        return ReadServiceMemory(framed, lifecycle, gc, out _);
    }

    private static ServiceMemoryInspection? ReadServiceMemory(NativeAotThreadingOutput.Capture framed,
        bool lifecycle, ServiceGcInspection? gc, out HashSet<int> positions)
    {
        const string marker = "M16_014_MEMORY";
        Need(gc.HasValue == lifecycle && !framed.Stdout.Contains(marker, StringComparison.Ordinal) &&
            !framed.Stderr.Contains(marker, StringComparison.Ordinal), "service_memory_channel");
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_memory_records");
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is >0 and <=1_000_000 && rows.All(r => r.Length is >0 and <=4096 && r.All(c => c is >= ' ' and <= '~')), "service_memory_records");
        var calls = ValidateLinuxServiceCallOrdinals(rows);
        var returned = ReadLinuxServiceCallReturns(rows).Select(r => r.RecordIndex).ToHashSet();
        var found = new HashSet<int>();
        var samples = new List<(int Position, Dictionary<string, ulong> Memory, Dictionary<string, ulong> Call)>();
        bool MemoryCall(ulong nr) => nr is 9 or 10 or 11 or 12 or 28 || lifecycle && nr == 25;
        for (int index = 0; index < rows.Length; index++)
        {
            if (!rows[index].Contains(marker, StringComparison.Ordinal)) continue;
            var memory = ReadRecord(rows[index], marker, "nr:dec", "regions:dec", "regionLimit:dec", "backed:dec", "pool:dec", "reserved:dec");
            Need(returned.Contains(index + 1), "service_memory_observation_call");
            var call = ReadRecord(calls[index + 1], "M16_008_CALL", WebCallFields);
            Need(MemoryCall(memory["nr"]) && call["nr"] == memory["nr"] && call["action"] == 1 && call["status"] == 1 &&
                (lifecycle || call["valueBits"] == unchecked((ulong)-12L)) &&
                memory["regionLimit"] == LinuxMemoryServices.WebMaximumRegions && memory["regions"] <= memory["regionLimit"] &&
                memory["pool"] == LinuxMemoryServices.MaximumPoolPages && memory["backed"] <= memory["pool"] &&
                memory["reserved"] <= 0x400000000 && memory["reserved"] % 4096 == 0 && memory["backed"] * 4096 <= memory["reserved"] &&
                (memory["regions"] != 0 || memory["reserved"] == 0), "service_memory_observation");
            found.Add(index); samples.Add((index, memory, call));
        }
        foreach (int index in returned)
        {
            var call = ReadRecord(calls[index], "M16_008_CALL", WebCallFields);
            if (call["action"] == 1 && MemoryCall(call["nr"]) && (lifecycle || call["valueBits"] == unchecked((ulong)-12L)))
                Need(found.Contains(index - 1), "service_memory_observation_missing");
        }
        if (!lifecycle) { positions = found; return null; }
        Need(samples.Count > 0, "service_memory_observation_missing");
        var budget = gc!.Value;
        Need(samples.All(s => s.Memory["backed"] * 4096 <= budget.PeakBackedBytes), "service_memory_peak_coverage");

        // Framing owns byte lengths/channels. Map stderr marker bytes back to
        // original normalized write positions, including split/UTF8 writes.
        var writes = new List<(int Position, int End)>(); int write = 0, errorBytes = 0;
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].StartsWith("M16_005_WRITE fd=", StringComparison.Ordinal))
            {
                Need(write < framed.WriteLengths.Count, "service_memory_write_count");
                if (framed.WriteChannels[write] == 2) { errorBytes += framed.WriteLengths[write]; writes.Add((i, errorBytes)); }
                write++;
            }
        Need(write == framed.WriteLengths.Count && errorBytes == Encoding.UTF8.GetByteCount(framed.Stderr), "service_memory_write_count");
        int Boundary(string prefix, bool complete)
        {
            int at = framed.Stderr.IndexOf(prefix, StringComparison.Ordinal);
            Need(at >= 0 && (at == 0 || framed.Stderr[at - 1] == '\n') &&
                framed.Stderr.IndexOf(prefix, at + prefix.Length, StringComparison.Ordinal) < 0, "service_memory_marker");
            int end = framed.Stderr.IndexOf('\n', at); Need(end >= 0, "service_memory_marker");
            int bytes = Encoding.UTF8.GetByteCount(framed.Stderr.AsSpan(0, complete ? end + 1 : at + 1));
            foreach (var item in writes) if (item.End >= bytes) return item.Position;
            throw new ToolFailure("service_memory_marker");
        }
        int firstGc = Boundary("CLROS_SERVICE_GC_BEGIN ", true), firstNative = Boundary("CLROS_SERVICE_NATIVE_BEGIN ", false),
            ordinaryStart = Boundary("CLROS_SERVICE_NATIVE_BEGIN ", true), ordinaryEnd = Boundary("CLROS_SERVICE_LIFECYCLE_END ", false),
            lastGc = Boundary("CLROS_SERVICE_GC_END ", true), lastNative = Boundary("CLROS_SERVICE_NATIVE_END ", false);
        Need(firstGc < firstNative && ordinaryStart < ordinaryEnd && ordinaryEnd <= lastGc && lastGc < lastNative, "service_memory_window");
        var seed = samples.LastOrDefault(s => s.Position < ordinaryStart);
        Need(seed.Memory is not null, "service_memory_window_seed");
        ulong ordinary = seed.Memory!["backed"];
        foreach (var sample in samples.Where(s => s.Position > ordinaryStart && s.Position < ordinaryEnd))
            ordinary = Math.Max(ordinary, sample.Memory["backed"]);
        int physical = 0;
        foreach (var window in new[] { (Start: firstGc, End: firstNative, Native: budget.NativeBegin), (Start: lastGc, End: lastNative, Native: budget.NativeEnd) })
        {
            var failures = samples.Select((s, i) => (Sample: s, Index: i)).Where(item => item.Sample.Position > window.Start && item.Sample.Position < window.End &&
                item.Sample.Call["nr"] == 9 && item.Sample.Call["a0"] == 0 && item.Sample.Call["a1"] == 1048576 && item.Sample.Call["a2"] == 3 &&
                item.Sample.Call["a3"] == 0x22 && (uint)item.Sample.Call["a4"] == uint.MaxValue && item.Sample.Call["a5"] == 0 &&
                item.Sample.Call["valueBits"] == unchecked((ulong)-12L)).ToArray();
            Need(failures.Length == 1 && failures[0].Index > 0, "service_memory_native_failure");
            var failed = failures[0].Sample.Memory; var before = samples[failures[0].Index - 1].Memory;
            Need(failed["backed"] == before["backed"] && failed["reserved"] == before["reserved"] && failed["regions"] == before["regions"] &&
                window.Native.RetainedBytes <= failed["backed"] * 4096, "service_memory_native_preservation");
            if (failed["pool"] - failed["backed"] < 256) physical++;
        }
        ulong ordinaryBytes = ordinary * 4096, poolBound = budget.GcLimitBytes + ordinaryBytes,
            entitlementBound = budget.StaticBytes + poolBound;
        positions = found;
        return new(samples.Count, ordinaryBytes, samples.Max(s => s.Memory["regions"]), physical, 2 - physical,
            poolBound, entitlementBound, poolBound <= budget.PoolBytes && entitlementBound <= budget.EntitlementBytes);
    }
}
