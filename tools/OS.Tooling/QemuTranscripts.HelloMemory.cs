using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class HelloMemory
    {
        private List<HelloRange> ranges = [];
        private readonly HelloRange[] image;
        private readonly uint poolPages;
        private HelloRange? imageProtection;
        internal unsafe HelloMemory(byte[] elf, uint poolPages = LinuxMemoryServices.ApplicationPoolPages)
        {
            Need(poolPages is LinuxMemoryServices.ApplicationPoolPages or OS.Manifest.LinuxAppConfiguration.ConfiguredServicePoolPages, "hello_memory_pool");
            this.poolPages = poolPages;
            fixed (byte* source = elf)
            {
                Need(LinuxElfLoader.Validate(source, (uint)elf.Length, 64 * 1024 * 1024, 1_572_864, out var plan) == LinuxElfLoadStatus.Ready, "hello_model_elf");
                for (uint i = 0; i < plan.LoadCount; i++)
                {
                    uint at = i * 5;
                    ulong start = plan.Loads[at] & ~4095UL;
                    ulong flags = plan.Loads[at + 4];
                    uint protection = ((flags & 4) != 0 ? 1U : 0) | ((flags & 2) != 0 ? 2U : 0) | ((flags & 1) != 0 ? 4U : 0);
                    ranges.Add(new(start, Round(plan.Loads[at] + plan.Loads[at + 3]), protection, false));
                }
            }

            image = ranges.ToArray();
            ranges.Add(new(0x70000000 - 1_572_864, 0x70000000, 3, false));
            ranges.Add(new(0x400000000, 0x400200000, 0, true));
        }

        internal static ulong Round(ulong bytes)
        {
            Need(bytes <= 0x1000000000 - 4095, "hello_memory_extent");
            return (bytes + 4095) & ~4095UL;
        }

        internal static bool Overlap(ulong a, ulong n, ulong b, ulong m) => (decimal)a + n > b && (decimal)b + m > a;
        internal bool Covered(ulong start, ulong bytes, uint protection)
        {
            if (bytes == 0 || start < 4096 || start >= 0x1000000000 || bytes > 0x1000000000 - start)
                return false;
            ulong cursor = start, end = start + bytes;
            foreach (var r in EffectiveRanges().OrderBy(r => r.Start))
            {
                if (r.End <= cursor)
                    continue;
                if (r.Start > cursor || (r.Protection & protection) != protection)
                    return false;
                cursor = Math.Min(end, r.End);
                if (cursor == end)
                    return true;
            }

            return false;
        }

        internal HelloRange DynamicWritable(ulong address)
        {
            var found = ranges.Where(r => r.Dynamic && r.Protection == 3 && address >= r.Start && address < r.End).ToArray();
            Need(found.Length == 1, "hello_dynamic_stack");
            return found[0];
        }

        internal void Map(ulong start, ulong bytes, uint protection, bool replace)
        {
            bytes = Round(bytes);
            Dynamic(start, bytes);
            Need(replace || ranges.All(r => !Overlap(start, bytes, r.Start, r.End - r.Start)), "hello_mmap_overlap");
            if (replace)
                Unmap(start, bytes);
            ranges.Add(new(start, start + bytes, protection, true));
            Audit();
        }

        internal void Protect(ulong start, ulong bytes, uint protection, bool empty = false)
        {
            if (start < 0x100000000)
            {
                Need(CanProtectImage(start, bytes, protection, out _), "hello_image_protection");
                bytes = Round(bytes);
                HelloRange original = image.Single(r => start >= r.Start && start < r.End && bytes <= r.End - start);
                imageProtection = protection == original.Protection ? null : new(start, start + bytes, protection, false);
                return;
            }

            bytes = Round(bytes);
            if (empty && bytes == 0)
                return;
            Dynamic(start, bytes);
            Need(Covered(start, bytes, 0), "hello_protect_coverage");
            Rewrite(start, bytes, protection);
            Audit();
        }

        // Original ELF rows remain provenance. Only one exact non-executable
        // data window may differ; another window cannot supersede that owner.
        internal bool CanProtectImage(ulong start, ulong bytes, uint protection, out long error)
        {
            error = -22;
            if (protection is not (1 or 3) || start < 4096 || start >= 0x100000000 || (start & 4095) != 0 || bytes == 0 || bytes > 0x1000000000 - 4095)
                return false;
            bytes = Round(bytes);
            if (!image.Any(r => r.Protection is 1 or 3 && start >= r.Start && start < r.End && bytes <= r.End - start))
                return false;
            if (imageProtection is { } prior && (prior.Start != start || prior.End - prior.Start != bytes))
            {
                error = -12;
                return false;
            }

            error = 0;
            return true;
        }

        private IEnumerable<HelloRange> EffectiveRanges()
        {
            foreach (HelloRange r in ranges)
            {
                if (imageProtection is not { } window || r.Dynamic || !Overlap(r.Start, r.End - r.Start, window.Start, window.End - window.Start))
                {
                    yield return r;
                    continue;
                }

                if (r.Start < window.Start)
                    yield return r with
                    {
                        End = window.Start
                    };
                yield return window;
                if (r.End > window.End)
                    yield return r with
                    {
                        Start = window.End
                    };
            }
        }

        internal void Unmap(ulong start, ulong bytes)
        {
            bytes = Round(bytes);
            Dynamic(start, bytes);
            Rewrite(start, bytes, null);
            Audit();
        }

        private static void Dynamic(ulong start, ulong bytes) => Need(start >= 0x100000000 && start < 0x1000000000 && (start & 4095) == 0 && bytes > 0 && bytes <= 0x1000000000 - start, "hello_dynamic_extent");
        private void Rewrite(ulong start, ulong bytes, uint? protection)
        {
            List<HelloRange> next = [];
            ulong end = start + bytes;
            foreach (var r in ranges)
            {
                if (!Overlap(start, bytes, r.Start, r.End - r.Start))
                {
                    next.Add(r);
                    continue;
                }

                Need(r.Dynamic, "hello_static_mutation");
                if (r.Start < start)
                    next.Add(r with
                    {
                        End = start
                    });
                if (protection.HasValue)
                    next.Add(new(Math.Max(start, r.Start), Math.Min(end, r.End), protection.Value, true));
                if (r.End > end)
                    next.Add(r with
                    {
                        Start = end
                    });
            }

            ranges = next;
        }

        private void Audit() => Need(ranges.Where(r => r.Dynamic).Sum(r => (decimal)(r.End - r.Start)) <= 0x400000000 && ranges.Where(r => r.Dynamic && r.Protection != 0).Sum(r => (decimal)(r.End - r.Start)) <= poolPages * 4096UL, "hello_memory_quota");
    }
}
