using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class HelloMemory
    {
        // Optional quota attribution replays actual committed backing separately
        // from the older virtual-access model. No physical addresses or allocator
        // placement are inferred. Every mutation must join a successful original
        // CALL; the complete protocol and native owner remain authoritative.
        internal sealed partial class DynamicBacking
        {
            private List<Extent> extents = [new(0x400000000, 0x400200000, 0, false, 0)];
            private readonly Dictionary<int, Origin> origins = [];
            private readonly List<ProtectionCall> protections = [];
            private ulong currentBreak = 0x400000000;
            internal IReadOnlyList<Extent> Extents => extents;
            internal IReadOnlyList<ProtectionCall> Protections => protections;
            internal ulong BackedBytes => extents.Where(r => r.Backed).Aggregate(0UL, (n, r) => n + r.End - r.Start);

            internal Origin GetOrigin(int id) => origins.TryGetValue(id, out var value) ? value : throw new ToolFailure("pthread_origin");
            internal ulong Backed(int origin, ulong start, ulong bytes) => extents.Where(r => r.Origin == origin && r.Backed && Overlap(start, bytes, r.Start, r.End - r.Start)).Aggregate(0UL, (n, r) => n + Math.Min(start + bytes, r.End) - Math.Max(start, r.Start));
            internal bool Covered(int origin, ulong start, ulong bytes, uint protection)
            {
                if (bytes == 0)
                    return true;
                ulong cursor = start, end = start + bytes;
                foreach (var r in extents.Where(r => r.Origin == origin).OrderBy(r => r.Start))
                {
                    if (r.End <= cursor)
                        continue;
                    if (r.Start > cursor || r.Protection != protection)
                        return false;
                    cursor = Math.Min(end, r.End);
                    if (cursor == end)
                        return true;
                }

                return false;
            }

            internal int OriginOf(ulong start, ulong bytes)
            {
                var found = extents.Where(r => Overlap(start, bytes, r.Start, r.End - r.Start)).Select(r => r.Origin).Distinct().ToArray();
                Need(found.Length == 1 && found[0] > 0, "pthread_mapping_identity");
                int id = found[0];
                ulong cursor = start;
                foreach (var r in extents.Where(r => r.Origin == id).OrderBy(r => r.Start))
                {
                    if (r.End <= cursor)
                        continue;
                    Need(r.Start <= cursor, "pthread_mapping_hole");
                    cursor = Math.Min(start + bytes, r.End);
                    if (cursor == start + bytes)
                        return id;
                }

                throw new ToolFailure("pthread_mapping_hole");
            }

            internal void Observe(Dictionary<string, ulong> c, int position)
            {
                if (c["action"] != 1 || c["status"] != 1)
                    return;
                ulong nr = c["nr"], a = c["a0"], n = c["a1"], p = c["a2"], v = c["valueBits"];
                if (nr == 9 && v is >= 0x100000000 and < 0x1000000000)
                {
                    n = Round(n);
                    Range(v, n);
                    Need(p is 0 or 1 or 3, "pthread_map_protection");
                    if ((c["a3"] & 0x10) == 0)
                        Need(extents.All(r => !Overlap(v, n, r.Start, r.End - r.Start)), "pthread_map_overlap");
                    else
                        Rewrite(v, n, _ => null);
                    int id = checked(position + 1);
                    Need(id > 0 && origins.TryAdd(id, new(position, v, n, (uint)p, c["identity"], c["rip"], a == 0 && c["a3"] == 0x22 && (uint)c["a4"] == uint.MaxValue && c["a5"] == 0)), "pthread_map_origin");
                    extents.Add(new(v, v + n, (uint)p, p != 0, id));
                }
                else if (nr is 10 or 11 or 28 && v == 0 && a >= 0x100000000)
                {
                    n = Round(n);
                    Range(a, n);
                    if (nr == 11)
                        Rewrite(a, n, _ => null);
                    else
                    {
                        Need(Coverage(a, n), "pthread_mutation_coverage");
                        if (nr == 10)
                        {
                            Need(p is 0 or 1 or 3, "pthread_protection");
                            Rewrite(a, n, r => r with { Protection = (uint)p, Backed = r.Backed || p != 0 });
                            protections.Add(new(position, a, n, (uint)p, c["identity"], c["rip"]));
                        }
                        else
                        {
                            Need(p is 4 or 16 or 17, "pthread_advice");
                            if (p == 4)
                                Rewrite(a, n, r => r with { Backed = r.Protection != 0 && r.Backed });
                        }
                    }
                }
                else if (nr == 12 && a != 0 && v == a)
                {
                    Need(a is >= 0x400000000 and <= 0x400200000, "pthread_brk");
                    ulong before = Round(currentBreak), after = Round(a);
                    if (before != after)
                    {
                        ulong start = Math.Min(before, after), bytes = Math.Max(before, after) - start;
                        Need(Coverage(start, bytes) && extents.Where(r => Overlap(start, bytes, r.Start, r.End - r.Start)).All(r => r.Origin == 0), "pthread_brk_owner");
                        Rewrite(start, bytes, r => r with { Protection = after > before ? 3U : 0U, Backed = after > before });
                    }

                    currentBreak = a;
                }
                else if (nr == 25 && v == a && a >= 0x100000000)
                {
                    ulong old = Round(n), size = Round(p);
                    Range(a, old);
                    Range(a, size);
                    Need(c["a3"] == 0 && Coverage(a, old), "pthread_remap");
                    if (size < old)
                        Rewrite(a + size, old - size, _ => null);
                    else if (size > old)
                    {
                        var prior = extents.Where(r => Overlap(a, old, r.Start, r.End - r.Start)).ToArray();
                        Need(prior.Length > 0 && prior.All(r => r.Origin == prior[0].Origin && r.Protection == prior[0].Protection && r.Backed == prior[0].Backed) && extents.All(r => !Overlap(a + old, size - old, r.Start, r.End - r.Start)), "pthread_remap_growth");
                        extents.Add(prior[0] with
                        {
                            Start = a + old,
                            End = a + size
                        });
                    }
                }

                Audit();
            }

            internal void End() => extents.Clear();
            private bool Coverage(ulong start, ulong bytes)
            {
                ulong cursor = start, end = start + bytes;
                foreach (var r in extents.OrderBy(r => r.Start))
                {
                    if (r.End <= cursor)
                        continue;
                    if (r.Start > cursor)
                        return false;
                    cursor = Math.Min(end, r.End);
                    if (cursor == end)
                        return true;
                }

                return false;
            }

            private static void Range(ulong start, ulong bytes) => Need(start >= 0x100000000 && start % 4096 == 0 && bytes > 0 && start < 0x1000000000 && bytes <= 0x1000000000 - start, "pthread_backing_extent");
            private void Rewrite(ulong start, ulong bytes, Func<Extent, Extent?> change)
            {
                List<Extent> next = [];
                ulong end = start + bytes;
                foreach (var r in extents)
                {
                    if (!Overlap(start, bytes, r.Start, r.End - r.Start))
                    {
                        next.Add(r);
                        continue;
                    }

                    if (r.Start < start)
                        next.Add(r with
                        {
                            End = start
                        });
                    var middle = change(r with
                    {
                        Start = Math.Max(start, r.Start),
                        End = Math.Min(end, r.End)
                    });
                    if (middle.HasValue)
                        next.Add(middle.Value);
                    if (r.End > end)
                        next.Add(r with
                        {
                            Start = end
                        });
                }

                extents = next;
            }

            private void Audit()
            {
                var sorted = extents.OrderBy(r => r.Start).ToArray();
                Need(sorted.Length <= 4096, "pthread_backing_metadata_bound");
                for (int i = 0; i < sorted.Length; i++)
                    Need(sorted[i].Start % 4096 == 0 && sorted[i].End % 4096 == 0 && sorted[i].End > sorted[i].Start && (i == 0 || sorted[i - 1].End <= sorted[i].Start), "pthread_backing_overlap");
                Need(BackedBytes <= OS.Manifest.LinuxAppConfiguration.ConfiguredServicePoolPages * 4096UL, "pthread_backing_pool");
            }
        }
    }
}
