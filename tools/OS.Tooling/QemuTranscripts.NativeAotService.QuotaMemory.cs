using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow original framed v3-quota bytes and validate
    /// budget, app retention windows, every memory-return snapshot/topology and
    /// the existing strict GC-arena replay. Invalid or incomplete values throw;
    /// input remains unchanged. This seam supplies no source/index/ELF admission,
    /// complete scheduler/mapping, peer/packet, native physical or guest proof.
    /// Prepared-program admission separately binds the reviewed publication and
    /// sources; these copied values grant no such authority.</summary>

    public static ServiceQuotaMemoryInspection InspectNativeAotServiceQuotaMemoryForTests(ReadOnlySpan<byte> raw, ulong entitlementBytes) => ReadServiceQuotaMemory(NativeAotThreadingOutput.ReadService(raw), entitlementBytes, out _);
    private static ServiceQuotaMemoryInspection ReadServiceQuotaMemory(NativeAotThreadingOutput.Capture framed, ulong entitlementBytes, out HashSet<int> metadataPositions)
    {
        const string memoryMarker = "M16_014_MEMORY", topologyMarker = "M16_016_TOPOLOGY";
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_quota_memory_records");
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is > 0 and <= 1_000_000 && rows.All(r => r.Length is > 0 and <= 4096 && r.All(c => c is >= ' ' and <= '~')), "service_quota_memory_records");
        var output = InspectNativeAotServiceQuotaOutputForTests(framed.Stdout, framed.Stderr);
        foreach (string marker in new[]
        {
            memoryMarker,
            topologyMarker,
            "M16_016_BUDGET",
            "M16_016_MEMORY"
        }

        )
            Need(!framed.Stdout.Contains(marker, StringComparison.Ordinal) && !framed.Stderr.Contains(marker, StringComparison.Ordinal), "service_quota_memory_channel");
        int One(string prefix)
        {
            int[] found = Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(found.Length == 1, "service_quota_memory_once");
            return found[0];
        }

        int budgetAt = One("M16_016_BUDGET"), memoryAt = One("M16_016_MEMORY"), ownerAt = One("M16_008_OWNER_END "), clockAt = One("M16_008_BUDGET_END "), serviceAt = One("M16_014_END "), abortAt = One("M16_016_QUOTA_ABORT "), startupAt = One("TEST_START m16.014.startup");
        var budget = ReadRecord(rows[budgetAt], "M16_016_BUDGET", "gc:dec", "nativeReserve:dec", "static:dec", "pool:dec", "entitlement:dec");
        const ulong page = 4096, chunk = 1048576, pool = LinuxAppConfiguration.ConfiguredServicePoolPages * page;
        Need(entitlementBytes == NativeAotServicePackage.MemoryBytes && budget["entitlement"] == entitlementBytes && budget["gc"] == NativeAotServicePackage.LifecycleGcLimitBytes && budget["nativeReserve"] == LinuxAppConfiguration.ConfiguredServiceNativeReserveBytes && budget["pool"] == pool && budget["gc"] + budget["nativeReserve"] == pool && budget["static"] > 0 && budget["static"] % page == 0 && budget["static"] <= entitlementBytes - pool && budgetAt < startupAt && rows[startupAt] == "TEST_START m16.014.startup", "service_quota_memory_budget");
        var finalMemory = ReadRecord(rows[memoryAt], "M16_016_MEMORY", "poolPages:dec", "peakPages:dec", "endPages:dec");
        const string ownerSuffix = " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        Need(rows[ownerAt].EndsWith(ownerSuffix, StringComparison.Ordinal), "service_quota_memory_owner");
        var owner = ReadRecord(rows[ownerAt][..^ownerSuffix.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec", "userReserved:dec", "userBacked:dec");
        var clock = ReadRecord(rows[clockAt], "M16_008_BUDGET_END", "nanos:dec");
        const string serviceSuffix = " process=drained mappings=restored pool=scrubbed random=zero reservation=retained";
        Need(rows[serviceAt].EndsWith(serviceSuffix, StringComparison.Ordinal), "service_quota_memory_service_end");
        var service = ReadRecord(rows[serviceAt][..^serviceSuffix.Length], "M16_014_END", "identity:dec", "calls:dec", "started:dec", "nanos:dec", "reaped:dec");
        Need(abortAt < memoryAt && memoryAt + 1 == clockAt && clockAt + 1 == ownerAt && ownerAt + 1 == serviceAt && owner["exit"] == 99 && owner["reaped"] > 0 && owner["userReserved"] == 0 && owner["userBacked"] == 0 && service["identity"] > 0 && service["calls"] > 0 && service["reaped"] == owner["reaped"] && service["started"] <= clock["nanos"] && clock["nanos"] <= service["nanos"] && service["nanos"] <= long.MaxValue && finalMemory["poolPages"] == pool / page && finalMemory["peakPages"] is > 0 && finalMemory["peakPages"] <= pool / page && finalMemory["endPages"] == 0, "service_quota_memory_end");
        var calls = ValidateLinuxServiceCallOrdinals(rows);
        var returns = ReadLinuxServiceCallReturns(rows).ToDictionary(r => r.RecordIndex);
        var callValues = returns.Keys.ToDictionary(at => at, at => ReadRecord(calls[at], "M16_008_CALL", WebCallFields));
        var gc = ReadServiceGcBacking(rows, calls, returns.Keys.ToHashSet());
        Need(gc.Summary.ArenaIdentified, "service_quota_memory_gc_arena_unreviewed");
        // The real protocol consumes final MEMORY/BUDGET_END/OWNER_END itself.
        var positions = new HashSet<int>
        {
            budgetAt
        };
        var snapshots = new List<ServiceQuotaMemorySnapshot>();
        bool MemoryCall(ulong nr) => nr is 9 or 10 or 11 or 12 or 25 or 28;
        foreach (int index in returns.Keys.Order())
        {
            var call = callValues[index];
            if (call["action"] != 1 || !MemoryCall(call["nr"]))
                continue;
            Need(call["status"] == 1 && index >= 2 && returns[index].Identity == call["identity"] && returns[index].ValueBits == call["valueBits"], "service_quota_memory_call");
            var m = ReadRecord(rows[index - 2], memoryMarker, "nr:dec", "regions:dec", "regionLimit:dec", "backed:dec", "pool:dec", "reserved:dec");
            var t = ReadRecord(rows[index - 1], topologyMarker, "identity:dec", "ordinal:dec", "nr:dec", "pool:dec", "backed:dec", "free:dec", "freeRuns:dec", "largestFreeRun:dec", "regions:dec", "regionLimit:dec", "tables:dec", "tableLimit:dec");
            Need(m["nr"] == call["nr"] && t["nr"] == call["nr"] && t["identity"] == call["identity"] && t["ordinal"] == returns[index].Ordinal && m["pool"] == pool / page && m["backed"] <= m["pool"] && m["regions"] <= m["regionLimit"] && m["regionLimit"] == LinuxMemoryServices.WebMaximumRegions && m["reserved"] <= 0x400000000 && m["reserved"] % page == 0 && m["backed"] * page <= m["reserved"] && (m["regions"] != 0 || m["reserved"] == 0) && (m["backed"] == 0 || m["regions"] > 0) && t["pool"] == m["pool"] && t["backed"] == m["backed"] && t["regions"] == m["regions"] && t["regionLimit"] == m["regionLimit"] && t["free"] == t["pool"] - t["backed"] && t["largestFreeRun"] <= t["free"] && t["freeRuns"] <= t["free"] && t["freeRuns"] <= t["regions"] + 1 && (t["free"] == 0 ? t["freeRuns"] == 0 && t["largestFreeRun"] == 0 : t["freeRuns"] > 0 && t["largestFreeRun"] > 0 && t["free"] <= t["freeRuns"] * t["largestFreeRun"] && t["largestFreeRun"] + t["freeRuns"] - 1 <= t["free"]) && t["tableLimit"] is >= 4 and <= OS.Kernel.Memory.X64UserAddressSpace.MaximumTables && t["tables"] >= 4 && t["tables"] <= t["tableLimit"] && gc.At[index] <= m["backed"] * page, "service_quota_memory_snapshot");
            snapshots.Add(new(index - 2, t["identity"], t["ordinal"], t["nr"], m["reserved"], t["pool"], t["backed"], t["free"], t["freeRuns"], t["largestFreeRun"], t["regions"], t["regionLimit"], t["tables"], t["tableLimit"], gc.At[index]));
            Need(positions.Add(index - 2) && positions.Add(index - 1), "service_quota_memory_duplicate");
        }

        Need(snapshots.Count > 1 && snapshots.Select(s => s.TableLimit).Distinct().Count() == 1 && snapshots.Max(s => s.BackedPages) == finalMemory["peakPages"], "service_quota_memory_peak");
        for (int index = 0; index < rows.Length; index++)
            Need((!rows[index].Contains(memoryMarker, StringComparison.Ordinal) && !rows[index].Contains(topologyMarker, StringComparison.Ordinal)) || positions.Contains(index), "service_quota_memory_orphan");
        // Map complete app marker byte spans back to original write positions.
        // Payload text never selects kernel policy; these windows only join the
        // separately checked original calls to copied app progress observations.
        var writes = new List<(int Position, int End)>();
        int write = 0, errorBytes = 0;
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].StartsWith("M16_005_WRITE fd=", StringComparison.Ordinal))
            {
                Need(write < framed.WriteLengths.Count, "service_quota_memory_write_count");
                if (framed.WriteChannels[write] == 2)
                {
                    errorBytes += framed.WriteLengths[write];
                    writes.Add((i, errorBytes));
                }

                write++;
            }

        Need(write == framed.WriteLengths.Count && errorBytes == Encoding.UTF8.GetByteCount(framed.Stderr), "service_quota_memory_write_count");
        int Boundary(string prefix, bool complete)
        {
            int at = framed.Stderr.IndexOf(prefix, StringComparison.Ordinal);
            Need(at >= 0 && (at == 0 || framed.Stderr[at - 1] == '\n') && framed.Stderr.IndexOf(prefix, at + prefix.Length, StringComparison.Ordinal) < 0, "service_quota_memory_marker");
            int end = framed.Stderr.IndexOf('\n', at);
            Need(end >= 0, "service_quota_memory_marker");
            int bytes = Encoding.UTF8.GetByteCount(framed.Stderr.AsSpan(0, complete ? end + 1 : at + 1));
            foreach (var item in writes)
                if (item.End >= bytes)
                    return item.Position;
            throw new ToolFailure("service_quota_memory_marker");
        }

        int entry = Boundary("CLROS_APP_ENTRY ", false), begin = Boundary("CLROS_SERVICE_GC_BEGIN ", true), pressure = Boundary("CLROS_SERVICE_QUOTA_BEGIN ", true);
        Need(startupAt < entry && gc.Summary.ReservationCallPosition < entry && entry <= begin && begin <= pressure, "service_quota_memory_window");
        ulong startupCommitted = Math.Max(output.StartupGc.BaselineCommittedBytes, Math.Max(output.StartupGc.LiveCommittedBytes, output.StartupGc.PressureCommittedBytes));
        Need(snapshots.Any(s => s.RecordIndex < begin) && startupCommitted <= snapshots.Where(s => s.RecordIndex < begin).Max(s => s.BackedPages) * page, "service_quota_memory_gc_coverage");
        var retained = new List<(ulong Address, int Call)>();
        ulong deniedValue = unchecked((ulong)-12L);
        bool PressureMap(Dictionary<string, ulong> call) => call["nr"] == 9 && call["a0"] == 0 && call["a1"] == chunk && call["a2"] == 3 && call["a3"] == 0x22 && (uint)call["a4"] == uint.MaxValue && call["a5"] == 0 && call["action"] == 1 && call["status"] == 1;
        for (ulong i = 0; i < output.CompletedChunks; i++)
        {
            int attempt = Boundary("CLROS_SERVICE_QUOTA_ATTEMPT index=" + i + " ", true), done = Boundary("CLROS_SERVICE_QUOTA_RETAINED index=" + i + " ", false);
            var matches = returns.Keys.Where(at => at > attempt && at < done).Where(at =>
            {
                var call = callValues[at];
                return PressureMap(call) && call["valueBits"] is >= 0x100000000 and < 0x1000000000;
            }).ToArray();
            Need(pressure <= attempt && attempt < done && matches.Length == 1, "service_quota_memory_retained_call");
            var mapping = callValues[matches[0]];
            ulong address = mapping["valueBits"];
            Need(address % page == 0 && chunk <= 0x1000000000 - address && !HelloMemory.Overlap(address, chunk, gc.Summary.ArenaStart, gc.Summary.ArenaBytes) && retained.All(prior => !HelloMemory.Overlap(prior.Address, chunk, address, chunk)), "service_quota_memory_retained_owner");
            retained.Add((address, matches[0]));
        }

        int finalCall = returns.Keys.Max(), lastAttempt = Boundary("CLROS_SERVICE_QUOTA_ATTEMPT index=" + output.CompletedChunks + " ", true);
        var denied = callValues[finalCall];
        Need(PressureMap(denied) && denied["valueBits"] == deniedValue && finalCall > lastAttempt && finalCall < abortAt && service["nanos"] >= denied["clock"], "service_quota_memory_denial");
        const string abortSuffix = " exit=99 policy=terminate cause=physical-backing";
        Need(rows[abortAt].EndsWith(abortSuffix, StringComparison.Ordinal), "service_quota_memory_abort");
        var abort = ReadRecord(rows[abortAt][..^abortSuffix.Length], "M16_016_QUOTA_ABORT", "identity:dec", "ordinal:dec", "nr:dec", "backed:dec", "pool:dec");
        var last = snapshots[^1];
        var previous = snapshots[^2];
        Need(last.RecordIndex + 2 == finalCall && last.Identity == denied["identity"] && last.Ordinal == returns[finalCall].Ordinal && last.Number == 9 && abort["identity"] == last.Identity && abort["ordinal"] == last.Ordinal && abort["nr"] == 9 && abort["backed"] == last.BackedPages && abort["pool"] == last.PoolPages && last.FreePages < chunk / page && last.BackedPages == previous.BackedPages && last.ReservedBytes == previous.ReservedBytes && last.Regions == previous.Regions && last.FreeRuns == previous.FreeRuns && last.LargestFreeRunPages == previous.LargestFreeRunPages && last.Tables == previous.Tables && output.RetainedBytes + last.GcBackedBytes <= last.BackedPages * page, "service_quota_memory_denial_preservation");
        foreach (int at in returns.Keys.Order())
        {
            var c = callValues[at];
            if (c["action"] != 1 || c["status"] != 1)
                continue;
            ulong nr = c["nr"], a = c["a0"], n = c["a1"], v = c["valueBits"];
            foreach (var held in retained.Where(h => h.Call < at))
            {
                bool Hit(ulong address, ulong bytes) => HelloMemory.Overlap(address, bytes, held.Address, chunk);
                bool changed = nr switch
                {
                    9 => v is >= 0x100000000 and < 0x1000000000 && Hit(v, HelloMemory.Round(n)),
                    10 or 11 => v == 0 && Hit(a, HelloMemory.Round(n)),
                    28 => v == 0 && c["a2"] is 4 or 8 && Hit(a, HelloMemory.Round(n)),
                    25 => v is >= 0x100000000 and < 0x1000000000 && (Hit(a, HelloMemory.Round(n)) || Hit(v, HelloMemory.Round(c["a2"]))),
                    _ => false
                };
                Need(!changed, "service_quota_memory_retained_mutation");
            }
        }

        // Eager RW mmap backing is part of the independently audited kernel
        // contract. Reject copied owner snapshots inconsistent with its still
        // retained spans at ANY memory return, not merely at final denial.
        // This necessary consistency bound is not physical allocation proof.
        foreach (var sample in snapshots)
        {
            ulong retainedVirtualBytes = (ulong)retained.Count(held => held.Call <= sample.RecordIndex + 2) * chunk;
            Need(retainedVirtualBytes <= sample.BackedPages * page - sample.GcBackedBytes, "service_quota_memory_retained_snapshot");
        }

        var during = snapshots.Where(s => s.RecordIndex > pressure).ToArray();
        Need(during.Length > 1 && retained.Count == (int)output.CompletedChunks, "service_quota_memory_pressure");
        ulong nonGc = snapshots.Max(s => s.BackedPages * page - s.GcBackedBytes), conservativePool = budget["gc"] + nonGc;
        metadataPositions = positions;
        return new(budget["gc"], budget["nativeReserve"], budget["static"], pool, entitlementBytes, finalMemory["peakPages"] * page, output.StartupGc, gc.Summary, Array.AsReadOnly(snapshots.ToArray()), during.Length, during.Max(s => s.BackedPages) * page, (ulong)retained.Count, output.RetainedBytes, last, nonGc, conservativePool, budget["static"] + conservativePool)
        {
            RetainedCalls = Array.AsReadOnly(retained.Select((held, index) => new ServiceQuotaRetainedMapping(index, held.Address, chunk, held.Call, callValues[held.Call]["identity"], returns[held.Call].Ordinal)).ToArray())
        };
    }
}
