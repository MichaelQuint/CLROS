namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied backing of the pinned service runtime's dedicated GC
    /// arena. Counts only still-owned arena pages; bookkeeping elsewhere remains
    /// non-GC conservatively. Supplied rows confer no source, image, allocation
    /// or guest authority. The parent binds the original image and startup.</summary>
    public readonly record struct ServiceGcBackingInspection(bool ArenaIdentified, ulong ArenaStart, ulong ArenaBytes,
        ulong LastBackedBytes, ulong PeakBackedBytes, int RevokedPages, int ReservationCallPosition);

    /// <summary>Synchronously inspect borrowed framed canonical call values.
    /// Invalid arena sequences or impossible backing throw; return values only.
    /// Original native/source review and complete replay remain independent.</summary>
    public static ServiceGcBackingInspection InspectNativeAotServiceGcBackingForTests(ReadOnlySpan<byte> raw)
    {
        var framed = NativeAotThreadingOutput.ReadService(raw);
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_gc_backing_records");
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is >0 and <=1_000_000 && rows.All(r => r.Length is >0 and <=4096), "service_gc_backing_records");
        return ReadServiceGcBacking(rows, ValidateLinuxServiceCallOrdinals(rows),
            ReadLinuxServiceCallReturns(rows).Select(r => r.RecordIndex).ToHashSet()).Summary;
    }

    private static (ServiceGcBackingInspection Summary, Dictionary<int, ulong> At) ReadServiceGcBacking(
        string[] rows, string[] calls, HashSet<int> returned)
    {
        var ledger = new ServiceGcBacking(); var at = new Dictionary<int, ulong>();
        var ordinals = ReadLinuxServiceCallReturns(rows).ToDictionary(r => r.RecordIndex, r => r.Ordinal);
        for (int i = 0; i < rows.Length; i++)
            if (returned.Contains(i))
            {
                var call = ReadRecord(calls[i], "M16_008_CALL", WebCallFields);
                ledger.Observe(call, ordinals[i], i);
                if (call["nr"] is 9 or 10 or 11 or 12 or 25 or 28) at.Add(i, ledger.BackedBytes);
            }
        return (ledger.Inspection, at);
    }

    // Original ELF3F1EF78C and runtime VMR820230c386db5b0106c5430b3e5aee92a7826bf5:
    // GCHeap.Initialize -> initialize_gc -> virtual_alloc -> VirtualReserve.
    // Default region arena is5*hard-limit; libc alignment adds one page, then
    // VirtualReserve trims it and excludes the exact arena from dumps. Shared
    // libc RIP alone is insufficient: require this complete startup sequence.
    private sealed class ServiceGcBacking
    {
        private const ulong Limit = NativeAotServicePackage.LifecycleGcLimitBytes, Bytes = 5 * Limit, Page = 4096;
        private const ulong Mmap = 0x47059a, Munmap = 0x470691, Madvise = 0x470541, Mprotect = 0x47064a;
        private const byte Owned = 1, Backed = 2, Protection = 12;
        private byte[] pages = [];
        private ulong reservation, start, end, identity, ordinal, backed, peak;
        private int stage, position = -1, revoked;
        internal ulong BackedBytes => backed * Page;
        internal ServiceGcBackingInspection Inspection => new(stage == 3, start, stage == 3 ? Bytes : 0,
            BackedBytes, peak * Page, revoked, position);

        internal void Observe(Dictionary<string, ulong> c, ulong callOrdinal, int row)
        {
            ulong nr = c["nr"], a = c["a0"], n = c["a1"], p = c["a2"], v = c["valueBits"];
            bool success = c["action"] == 1 && c["status"] == 1;
            if (stage is 1 or 2)
            {
                if (c["identity"] != identity)
                {
                    // Other workers may run between this worker's consecutive
                    // calls. Never classify an arena they could have changed.
                    bool Overlap(ulong address, ulong bytes) => bytes > ulong.MaxValue - (Page - 1) ||
                        address > ulong.MaxValue - ((bytes + Page - 1) & ~(Page - 1)) ||
                        bytes != 0 && address < reservation + Bytes + Page &&
                        reservation < address + ((bytes + Page - 1) & ~(Page - 1));
                    bool touches = success && (nr switch
                    {
                        9 => v is >=0x100000000 and <0x1000000000 && Overlap(v, n),
                        10 or 11 or 28 => v == 0 && Overlap(a, n),
                        25 => v is >=0x100000000 and <0x1000000000 && (Overlap(a, n) || Overlap(v, p)),
                        12 => a != 0 && v is >=0x100000000 and <0x1000000000,
                        _ => false
                    });
                    Need(!touches, "service_gc_arena_interference"); return;
                }
                Need(success && callOrdinal == ordinal + (ulong)stage && v == 0,
                    "service_gc_arena_sequence");
                if (stage == 1)
                {
                    Need(nr == 11 && a == reservation && n == Page && c["rip"] == Munmap, "service_gc_arena_trim");
                    start = reservation + Page; end = start + Bytes; stage = 2;
                }
                else
                {
                    Need(nr == 28 && a == start && n == Bytes && p == 16 && c["rip"] == Madvise, "service_gc_arena_dump");
                    pages = new byte[checked((int)(Bytes / Page))]; Array.Fill(pages, Owned); stage = 3;
                }
                return;
            }
            if (!success) return;
            if (nr == 9 && a == 0 && n == Bytes + Page && p == 0 && c["a3"] == 0x22 &&
                (uint)c["a4"] == uint.MaxValue && c["a5"] == 0 && c["rip"] == Mmap &&
                v is >=0x100000000 and <0x1000000000)
            {
                Need(stage == 0 && v % Page == 0 && Bytes + Page <= 0x1000000000 - v, "service_gc_arena_unique");
                reservation = v; identity = c["identity"]; ordinal = callOrdinal; position = row; stage = 1; return;
            }
            if (stage != 3) return;
            if (nr == 9 && v is >=0x100000000 and <0x1000000000)
                Change(v, n, a == v && p == 0 && c["a3"] == 0x32 && (uint)c["a4"] == uint.MaxValue &&
                    c["a5"] == 0 && c["rip"] == Mmap && Inside(v, n) ? Operation.Decommit : Operation.Revoke);
            else if (nr == 10 && v == 0)
                Change(a, n, c["rip"] == Mprotect && p is 0 or 1 or 3 && Inside(a, n) ? Operation.Protect : Operation.Revoke, p);
            else if (nr == 11 && v == 0) Change(a, n, Operation.Revoke);
            else if (nr == 28 && v == 0 && p == 4)
                Change(a, n, c["rip"] == Madvise && Inside(a, n) ? Operation.Advise : Operation.Revoke);
            else if (nr == 28 && v == 0 && p is not (16 or 17)) Change(a, n, Operation.Revoke);
            else if (nr == 25 && v is >=0x100000000 and <0x1000000000)
            { Change(a, n, Operation.Revoke); Change(v, p, Operation.Revoke); }
            Need(backed * Page <= Limit, "service_gc_backing_limit");
        }

        private enum Operation { Revoke, Protect, Decommit, Advise }
        private bool Inside(ulong a, ulong n) => a >= start && a < end && n > 0 && n <= end - a;
        private void Change(ulong a, ulong n, Operation operation, ulong protection = 0)
        {
            Need(a % Page == 0 && n is >0 and <=0x400000000 && a <= ulong.MaxValue - n - (Page - 1), "service_gc_backing_extent");
            ulong finish = a + ((n + Page - 1) & ~(Page - 1));
            for (ulong cursor = Math.Max(a, start); cursor < Math.Min(finish, end); cursor += Page)
            {
                int i = checked((int)((cursor - start) / Page)); byte prior = pages[i];
                if ((prior & Owned) == 0) continue;
                byte next = operation switch
                {
                    Operation.Revoke => 0,
                    Operation.Decommit => Owned,
                    Operation.Protect => (byte)((prior & ~Protection) | (byte)(protection << 2) | (protection != 0 ? Backed : 0)),
                    Operation.Advise => (prior & Protection) == 0 ? (byte)(prior & ~Backed) : prior,
                    _ => throw new ToolFailure("service_gc_backing_operation")
                };
                bool was = (prior & (Owned | Backed)) == (Owned | Backed), now = (next & (Owned | Backed)) == (Owned | Backed);
                if (was != now) { if (now) backed++; else backed--; }
                if (operation == Operation.Revoke) revoked++;
                pages[i] = next;
            }
            peak = Math.Max(peak, backed);
        }
    }
}
