using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Inspect bounded copied rows without mutation or execution.
    /// Production additionally joins every original memory topology snapshot
    /// and complete scheduler history, and retains original file/native/peer
    /// ownership. This seam constructs no capture or Completion authority.</summary>

    public static ServiceQuotaPthreadInspection InspectNativeAotServiceQuotaPthreadsForTests(string[] rows) => ReadServiceQuotaPthreads(rows, out _);
    private static ServiceQuotaPthread PthreadRow(string row, int at, Dictionary<string, ulong> call, ulong ordinal)
    {
        int split = row.LastIndexOf(" data=", StringComparison.Ordinal);
        Need(split > 0 && row[(split + 6)..].Length == 224 && row[(split + 6)..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "pthread_raw");
        byte[] data = Convert.FromHexString(row[(split + 6)..]);
        var r = ReadRecord(row[..split], "M16_016_PTHREAD", "identity:dec", "ordinal:dec", "child:dec", "childTid:dec", "tls:dec", "sp:dec", "parentTid:dec", "clearTid:dec", "rip:dec", "bytes:dec");
        ulong Word(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
        ulong tls = r["tls"], map = Word(0x48), bytes = Word(0x50), top = Word(0x58), size = Word(0x60), guard = Word(0x68);
        Need(call["nr"] == 56 && call["action"] == 1 && call["status"] == 1 && call["a0"] == 0x7d0f00 && call["rip"] == 0x479889 && r["bytes"] == 112 && r["identity"] == call["identity"] && r["ordinal"] == ordinal && r["childTid"] == call["valueBits"] && r["childTid"] is >= 2 and <= int.MaxValue && r["child"] != r["identity"] && (uint)r["child"] is >= 1 and <= 16 && r["child"] >> 32 > 0 && r["tls"] == call["a4"] && r["sp"] == call["a1"] && r["parentTid"] == call["a2"] && r["clearTid"] == call["a3"] && r["rip"] == call["rip"] && tls >= 0x100000000 && tls <= 0x1000000000 - 512 && r["parentTid"] == tls + 0x30 && Word(0) == tls && BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x30, 4)) == r["childTid"], "pthread_clone_join");
        Need(map >= 0x100000000 && map < 0x1000000000 && map % 4096 == 0 && bytes > 0 && bytes % 4096 == 0 && bytes <= 0x1000000000 - map && guard % 4096 == 0 && guard < bytes && size >= 128 && top >= size && top - size == map + guard && top <= tls && tls + 512 <= map + bytes && top >= 32 && r["sp"] == ((top - 32) & ~15UL) - 8 && r["sp"] >= map + guard + 128, "pthread_default_shape");
        return new(at, at - 1, r["identity"], ordinal, r["child"], (int)r["childTid"], tls, top - size, size, guard, map, bytes, -1, -1)
        {
            InitialStackPointer = r["sp"]
        };
    }

    private static ServiceQuotaPthreadInspection ReadServiceQuotaPthreads(string[] rows, out HashSet<int> metadata)
    {
        QuotaComponentInput(rows);
        metadata = [];
        var normalized = ValidateLinuxServiceCallOrdinals(rows);
        var returns = ReadLinuxServiceCallReturns(rows).ToDictionary(r => r.RecordIndex);
        var calls = returns.Keys.ToDictionary(at => at, at => ReadRecord(normalized[at], "M16_008_CALL", WebCallFields));
        bool Memory(Dictionary<string, ulong> c) => c["nr"] is 9 or 10 or 11 or 12 or 25 or 28;
        int[] owners = Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith("M16_008_OWNER_END", StringComparison.Ordinal)).ToArray();
        Need(owners.Length == 1, "pthread_owner");
        int end = owners[0];
        const string suffix = " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        Need(rows[end].EndsWith(suffix, StringComparison.Ordinal), "pthread_owner");
        var owner = ReadRecord(rows[end][..^suffix.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec", "userReserved:dec", "userBacked:dec");
        Need(owner["exit"] == 99 && owner["userReserved"] == 0 && owner["userBacked"] == 0 && calls.Keys.All(i => i < end), "pthread_owner");
        var backing = new HelloMemory.DynamicBacking();
        List<ServiceQuotaPthread> threads = [];
        for (int at = 0; at < rows.Length; at++)
        {
            if (calls.TryGetValue(at, out var call) && Memory(call))
                backing.Observe(call, at);
            if (!rows[at].StartsWith("M16_016_PTHREAD", StringComparison.Ordinal))
                continue;
            Need(at > 0 && at < end && calls.ContainsKey(at - 1), "pthread_orphan");
            var t = PthreadRow(rows[at], at, calls[at - 1], returns[at - 1].Ordinal);
            Need(!threads.Any(prior => prior.ChildIdentity == t.ChildIdentity || prior.Tid == t.Tid), "pthread_child_duplicate");
            int id = backing.OriginOf(t.MappingStart, t.MappingBytes);
            var origin = backing.GetOrigin(id);
            Need(origin.Anonymous && origin.Start == t.MappingStart && origin.Bytes == t.MappingBytes && origin.Rip == 0x4705da && origin.RecordIndex < t.CallRecordIndex && backing.Covered(id, t.MappingStart, t.GuardBytes, 0) && backing.Backed(id, t.MappingStart, t.GuardBytes) == 0 && backing.Covered(id, t.MappingStart + t.GuardBytes, t.MappingBytes - t.GuardBytes, 3) && backing.Backed(id, t.MappingStart + t.GuardBytes, t.MappingBytes - t.GuardBytes) == t.MappingBytes - t.GuardBytes, "pthread_mapping_provenance");
            bool previous = threads.Any(prior => prior.AllocationRecordIndex == origin.RecordIndex);
            Need(previous || origin.Identity == t.Identity, "pthread_allocation_parent");
            Need(t.GuardBytes == 0 ? origin.Protection == 3 : origin.Protection == 0 && backing.Protections.Any(p => p.RecordIndex > origin.RecordIndex && p.RecordIndex < t.CallRecordIndex && p.Start == t.MappingStart + t.GuardBytes && p.Bytes == t.MappingBytes - t.GuardBytes && p.Protection == 3 && p.Identity == origin.Identity && p.Rip == 0x47068a), "pthread_guard_provenance");
            foreach (var prior in threads.Where(p => p.AllocationRecordIndex == origin.RecordIndex))
                Need(prior.MappingStart == t.MappingStart && prior.MappingBytes == t.MappingBytes && prior.StackStart == t.StackStart && prior.StackBytes == t.StackBytes && prior.GuardBytes == t.GuardBytes, "pthread_cached_shape");
            threads.Add(t with
            {
                AllocationRecordIndex = origin.RecordIndex
            });
            metadata.Add(at);
        }

        Need(threads.Count > 0 && threads.Count == calls.Count(p => p.Value["nr"] == 56 && p.Value["action"] == 1 && p.Value["status"] == 1 && unchecked((long)p.Value["valueBits"]) > 0) && owner["reaped"] == (ulong)threads.Count + 1, "pthread_complete");
        for (int at = 0; at < rows.Length; at++)
        {
            if (!rows[at].StartsWith("M16_008_REAP ", StringComparison.Ordinal))
                continue;
            const string scrub = " context=zero stack=zero slot=zero";
            Need(rows[at].EndsWith(scrub, StringComparison.Ordinal), "pthread_reap");
            var r = ReadRecord(rows[at][..^scrub.Length], "M16_008_REAP", "old:dec", "next:dec");
            int index = threads.FindIndex(t => t.ChildIdentity == r["old"]);
            if (index < 0)
                continue;
            var t = threads[index];
            Need(t.ReapRecordIndex < 0 && at > t.RecordIndex && at < end && r["old"] <= ulong.MaxValue - (1UL << 32) && r["next"] == r["old"] + (1UL << 32), "pthread_reap");
            threads[index] = t with
            {
                ReapRecordIndex = at
            };
        }

        Need(threads.All(t => t.ReapRecordIndex > t.RecordIndex), "pthread_reap_missing");
        foreach (var group in threads.GroupBy(t => t.AllocationRecordIndex))
        {
            var ordered = group.OrderBy(t => t.RecordIndex).ToArray();
            for (int i = 1; i < ordered.Length; i++)
                Need(ordered[i - 1].ReapRecordIndex < ordered[i].CallRecordIndex, "pthread_live_alias");
        }

        // Replay again after attribution, so commits before the first clone
        // contribute to the true allocation peak and cached maps survive reap.
        backing = new();
        List<ServiceQuotaPthreadSnapshot> snapshots = [];
        var allocations = threads.GroupBy(t => t.AllocationRecordIndex + 1).ToDictionary(g => g.Key, g => g.First());
        var births = threads.ToDictionary(t => t.RecordIndex);
        var reaps = threads.ToDictionary(t => t.ReapRecordIndex);
        Dictionary<int, int> live = [];
        HashSet<int> seen = [];
        for (int at = 0; at <= end; at++)
        {
            bool memory = calls.TryGetValue(at, out var call) && Memory(call!);
            if (memory)
                backing.Observe(call!, at);
            if (births.TryGetValue(at, out var born))
            {
                int id = born.AllocationRecordIndex + 1;
                seen.Add(id);
                live[id] = live.GetValueOrDefault(id) + 1;
            }

            bool reap = reaps.TryGetValue(at, out var reapedThread);
            if (reap)
            {
                int id = reapedThread.AllocationRecordIndex + 1;
                Need(live.GetValueOrDefault(id) > 0, "pthread_live_count");
                live[id]--;
            }

            if (at == end)
                backing.End();
            if (!memory && !reap && !metadata.Contains(at) && at != end)
                continue;
            ulong stack = 0, guard = 0, tail = 0, retained = 0;
            foreach (var extent in backing.Extents)
            {
                if (!extent.Backed || !allocations.TryGetValue(extent.Origin, out var t))
                    continue;
                ulong Portion(ulong a, ulong n) => HelloMemory.Overlap(a, n, extent.Start, extent.End - extent.Start) ? Math.Min(a + n, extent.End) - Math.Max(a, extent.Start) : 0;
                ulong s = Portion(t.StackStart, t.StackBytes), g = Portion(t.MappingStart, t.GuardBytes), whole = extent.End - extent.Start;
                Need(s + g <= whole, "pthread_component_overlap");
                ulong rest = whole - s - g;
                stack += s;
                guard += g;
                tail += rest;
                if (seen.Contains(extent.Origin) && live.GetValueOrDefault(extent.Origin) == 0)
                    retained += whole;
            }

            Need(stack + guard + tail <= backing.BackedBytes, "pthread_backing_total");
            snapshots.Add(new(at, backing.BackedBytes, stack, guard, tail, stack + guard + tail, retained));
        }

        return new(Array.AsReadOnly(threads.ToArray()), Array.AsReadOnly(snapshots.ToArray()), snapshots.Max(s => s.StackBackedBytes), snapshots.Max(s => s.PthreadBackedBytes));
    }
}
