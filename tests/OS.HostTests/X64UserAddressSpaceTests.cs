using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static unsafe class X64UserAddressSpaceTests
{
    private const ulong Root = 0x1000000;
    private const uint Pages = 16;
    private static X64PageTableEncodingPreconditions Conditions()
    {
        HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406, 0, 3, 255, out var pat));
        return X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat);
    }
    private static X64UserMappingRange[] Ranges() =>
    [
        new() { VirtualStart = 0x100000, PhysicalStart = 0x100000, Pages = 1, Protection = PageProtection.Read | PageProtection.Execute, Cache = PageCachePolicy.WriteBack },
        new() { VirtualStart = 0x200000, PhysicalStart = 0x200000, Pages = 1, Protection = PageProtection.Read | PageProtection.Write, Cache = PageCachePolicy.WriteBack },
        new() { VirtualStart = 0x400000, PhysicalStart = 0x300000, Pages = 2, Protection = PageProtection.Read | PageProtection.Execute, Cache = PageCachePolicy.WriteBack, User = true },
        new() { VirtualStart = 0x6ffff000, PhysicalStart = 0x500000, Pages = 1, Protection = PageProtection.Read | PageProtection.Write, Cache = PageCachePolicy.WriteBack, User = true }
    ];
    private static ulong* Leaf(ulong* tables, ulong va)
    {
        uint page = 0;
        for (int level = 3; level > 0; level--)
            page = (uint)(((tables[page * 512 + ((va >> (12 + level * 9)) & 511)] & 0x000ffffffffff000) - Root) / 4096);
        return tables + page * 512 + ((va >> 12) & 511);
    }

    [HostTest("ordered admission matches scalar prefix checks for shuffled adjacent mappings through the host range limit")]
    private static void OrderedAdmission()
    {
        foreach (int count in new[] { 1, 2, 17, (int)X64UserAddressSpace.MaximumRanges })
            for (int seed = 0; seed < 8; seed++)
            {
                var ranges = new X64UserMappingRange[count];
                for (int i = 0; i < count; i++)
                    ranges[i] = Mapping(0x40000000 + (ulong)i * 4096, 0x2000000 + (ulong)(count - 1 - i) * 4096);
                var random = new Random(seed);
                for (int i = count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (ranges[i], ranges[j]) = (ranges[j], ranges[i]);
                }
                CheckAdmission(ranges, X64UserAddressSpaceStatus.Ready);
            }
        CheckAdmission(new X64UserMappingRange[(int)X64UserAddressSpace.MaximumRanges + 1], X64UserAddressSpaceStatus.InvalidInput);
    }

    [HostTest("ordered admission checks both neighbors for VA and physical duplicates containment adjacency and implicit arena")]
    private static void OverlapAdmission()
    {
        X64UserMappingRange[] prefix = [Mapping(0x40010000, 0x2010000), Mapping(0x40000000, 0x2000000, 3), Mapping(0x40008000, 0x2008000, 2)];
        foreach (var candidate in new[]
        {
            Mapping(0x40000000, 0x2100000), Mapping(0x41000000, 0x2000000),
            Mapping(0x40001000, 0x2100000), Mapping(0x41000000, 0x2001000),
            Mapping(0x3ffff000, 0x2100000, 5), Mapping(0x41000000, 0x1fff000, 5),
            Mapping(0x40007000, 0x2100000, 2), Mapping(0x41000000, 0x2007000, 2)
        })
            CheckAdmission([.. prefix, candidate], X64UserAddressSpaceStatus.OverlappingRanges);
        CheckAdmission([.. prefix, Mapping(0x40003000, 0x2003000, 5)], X64UserAddressSpaceStatus.Ready);
        foreach (var candidate in new[]
        {
            Mapping(Root, 0x2100000), Mapping(Root + 4096, 0x2100000),
            Mapping(0x41000000, Root), Mapping(0x41000000, Root + 4096),
            Mapping(Root - 4096, 0x2100000, Pages + 2), Mapping(0x41000000, Root - 4096, Pages + 2)
        })
            CheckAdmission([candidate], X64UserAddressSpaceStatus.OverlappingRanges);
        foreach (var candidate in new[]
        {
            Mapping(Root - 4096, 0x2100000), Mapping(Root + Pages * 4096, 0x2100000),
            Mapping(0x41000000, Root - 4096), Mapping(0x41000000, Root + Pages * 4096)
        })
            CheckAdmission([candidate], X64UserAddressSpaceStatus.Ready);
    }

    [HostTest("ordered admission retains input validation encoding overlap leaf and final arena error precedence")]
    private static void AdmissionPrecedence()
    {
        var first = Mapping(0x40000000, 0x2000000);
        var invalid = Mapping(0x41000000, 0x2100000, 0);
        CheckAdmission([first, first, invalid], X64UserAddressSpaceStatus.OverlappingRanges);
        var encoding = first;
        encoding.Protection |= PageProtection.Execute;
        CheckAdmission([first, encoding, invalid], X64UserAddressSpaceStatus.InvalidEncoding);
        encoding.Pages = 0;
        CheckAdmission([first, encoding], X64UserAddressSpaceStatus.InvalidRange);
        // The arena is the final candidate, not an eager test of the prefix.
        CheckAdmission([Mapping(Root, 0x2100000), invalid], X64UserAddressSpaceStatus.InvalidRange);
        var full = Mapping(0x40000000, 0x10000000, 32768);
        CheckAdmission([full, Mapping(0x50000000, 0x20000000), encoding], X64UserAddressSpaceStatus.InvalidRange);
        CheckAdmission([full], X64UserAddressSpaceStatus.InvalidRange);
        CheckAdmission([first, encoding], X64UserAddressSpaceStatus.InvalidRange, 4);
        CheckAdmission(Ranges(), X64UserAddressSpaceStatus.InsufficientTables, 4);
    }

    [HostTest("table coverage matches scalar bucket sets for shuffled sparse ranges sharing and crossing PT and directory boundaries")]
    private static void SharedBucketCoverage()
    {
        X64UserMappingRange[] ranges =
        [
            Mapping(0x40003000, 0x3000000, 2), Mapping(0x3ffff000, 0x4000000, 2),
            Mapping(0x401ff000, 0x5000000, 2), Mapping(0x40009000, 0x6000000, 2),
            Mapping(0x1ff000, 0x7000000, 2), Mapping(0x80005000, 0x8000000)
        ];
        // Seven distinct PT buckets (including the arena), three directories,
        // PML4 and PDPT. The sparse gaps must not consume table pages.
        for (int order = 0; order < 2; order++)
        {
            CheckAdmission(ranges, X64UserAddressSpaceStatus.Ready, 12);
            CheckAdmission(ranges, X64UserAddressSpaceStatus.InsufficientTables, 11);
            Array.Reverse(ranges);
        }
    }

    [HostTest("table coverage includes implicit arena first middle last and sharing a PT at exact capacity")]
    private static void ArenaBucketCoverage()
    {
        X64UserMappingRange[][] placements =
        [
            [Mapping(Root + 0x600000, 0x3000000), Mapping(Root + 0x200000, 0x4000000)],
            [Mapping(Root + 0x200000, 0x3000000), Mapping(Root - 0x400000, 0x4000000)],
            [Mapping(Root - 0x400000, 0x3000000), Mapping(Root - 0x800000, 0x4000000)]
        ];
        foreach (var ranges in placements)
        {
            CheckAdmission(ranges, X64UserAddressSpaceStatus.Ready, 6);
            CheckAdmission(ranges, X64UserAddressSpaceStatus.InsufficientTables, 5);
        }
        // Both later inputs share the arena's PT. In the second vector, the
        // arena lies between an earlier PT and an adjacent same-PT mapping.
        CheckAdmission([Mapping(Root + 0x20000, 0x3000000), Mapping(Root + 0x10000, 0x4000000)],
            X64UserAddressSpaceStatus.Ready, 4);
        X64UserMappingRange[] sharing = [Mapping(Root + 5 * 4096, 0x3000000), Mapping(Root - 4096, 0x4000000)];
        CheckAdmission(sharing, X64UserAddressSpaceStatus.Ready, 5);
        CheckAdmission(sharing, X64UserAddressSpaceStatus.InsufficientTables, 4);
    }

    [HostTest("table coverage matches scalar sets through final UserEnd bucket with holes and exact table capacity")]
    private static void LastBucketCoverage()
    {
        X64UserMappingRange[] ranges =
        [
            Mapping(X64UserAddressSpace.UserEnd - 4096, 0x3000000), Mapping(0x40000000, 0x4000000),
            Mapping(X64UserAddressSpace.UserEnd - 0x201000, 0x5000000, 2),
            Mapping(X64UserAddressSpace.UserEnd - 0x10000, 0x6000000)
        ];
        // The crossing pair and final-page mapping share the final PT and
        // directory. Its exclusive bucket end is 32768, not a wrapped index.
        CheckAdmission(ranges, X64UserAddressSpaceStatus.Ready, 9);
        CheckAdmission(ranges, X64UserAddressSpaceStatus.InsufficientTables, 8);
    }

    private static X64UserMappingRange Mapping(ulong va, ulong pa, ulong pages = 1) => new()
    {
        VirtualStart = va,
        PhysicalStart = pa,
        Pages = pages,
        Protection = PageProtection.Read | PageProtection.Write,
        Cache = PageCachePolicy.WriteBack,
        User = true
    };

    private static void CheckAdmission(X64UserMappingRange[] ranges, X64UserAddressSpaceStatus expected, uint pages = Pages)
    {
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(pages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)pages * 512).Clear();
            byte[] original = MemoryMarshal.AsBytes(ranges.AsSpan()).ToArray();
            fixed (X64UserMappingRange* values = ranges)
            {
                var scalar = ScalarAdmission(values, (uint)ranges.Length, tables, pages, conditions, out uint required, out ulong leaves);
                HostCheck.Equal(expected, scalar);
                var actual = X64UserAddressSpace.Build(values, (uint)ranges.Length, tables, Root, pages, conditions);
                HostCheck.Equal(scalar, actual.Status);
                HostCheck.True(original.AsSpan().SequenceEqual(MemoryMarshal.AsBytes(ranges.AsSpan())));
                if (actual.Succeeded)
                {
                    HostCheck.Equal(required, actual.UsedTables);
                    HostCheck.Equal(leaves, actual.LeafPages);
                    HostCheck.True(X64UserAddressSpace.Audit(values, (uint)ranges.Length, tables, Root, pages, conditions, actual));
                }
                else
                    HostCheck.True(new ReadOnlySpan<ulong>(tables, (int)pages * 512).IndexOfAnyExcept(0UL) < 0);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    // Retained scalar admission rule, independent of the production index orders.
    // Bool slot sets also independently check the required table/leaf accounting.
    private static X64UserAddressSpaceStatus ScalarAdmission(X64UserMappingRange* ranges, uint count, ulong* tables,
        uint pages, X64PageTableEncodingPreconditions conditions, out uint required, out ulong leaves)
    {
        required = 0;
        leaves = 0;
        if (ranges == null || ((ulong)ranges & 7) != 0 || count == 0 || count > X64UserAddressSpace.MaximumRanges ||
            (ulong)ranges > ~0UL - count * (ulong)sizeof(X64UserMappingRange) || tables == null || ((ulong)tables & 4095) != 0 ||
            pages < 4 || pages > X64UserAddressSpace.MaximumTables || Root < 4096 || (Root & 4095) != 0 || Root > X64UserAddressSpace.PhysicalEnd - pages * 4096UL ||
            (ulong)tables > ~0UL - pages * 4096UL || conditions.Validate() != X64PageTableEntryStatus.Success)
            return X64UserAddressSpaceStatus.InvalidInput;
        static bool Overlaps(ulong a, ulong aBytes, ulong b, ulong bBytes) => a < b + bBytes && b < a + aBytes;
        if (Overlaps((ulong)ranges, count * (ulong)sizeof(X64UserMappingRange), (ulong)tables, pages * 4096UL))
            return X64UserAddressSpaceStatus.OverlappingRanges;
        bool[] slots = new bool[32768], directories = new bool[64];
        required = 2;
        for (uint i = 0; i <= count; i++)
        {
            var range = i == count ? new X64UserMappingRange
            {
                VirtualStart = Root,
                PhysicalStart = Root,
                Pages = pages,
                Protection = PageProtection.Read | PageProtection.Write,
                Cache = PageCachePolicy.WriteBack
            } : ranges[i];
            bool highDevice = range.PhysicalStart >= X64UserAddressSpace.PhysicalEnd && !range.User && range.Pages == 1 &&
                range.VirtualStart == range.PhysicalStart && range.Cache == PageCachePolicy.Uncached &&
                (range.Protection == PageProtection.Read || range.Protection == (PageProtection.Read | PageProtection.Write));
            ulong physicalLimit = highDevice ? X64UserAddressSpace.UserEnd : X64UserAddressSpace.PhysicalEnd;
            if (range.Pages == 0 || range.Pages > 32768 || range.VirtualStart < 4096 || range.PhysicalStart < 4096 ||
                ((range.VirtualStart | range.PhysicalStart) & 4095) != 0 || range.VirtualStart > X64UserAddressSpace.UserEnd - range.Pages * 4096 ||
                (!range.User && !highDevice && range.VirtualStart > X64UserAddressSpace.PhysicalEnd - range.Pages * 4096) ||
                range.PhysicalStart > physicalLimit - range.Pages * 4096)
                return X64UserAddressSpaceStatus.InvalidRange;
            foreach (ulong page in new[] { 0UL, range.Pages - 1 })
            {
                if (PhysicalAddress.TryCreateFromHardwareRegisterBoundary(range.PhysicalStart + page * 4096, out var address) != AddressValueStatus.Success ||
                    !X64PageTableEntry.TryEncode(new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf, address, range.Protection, range.Cache), conditions).Succeeded)
                    return X64UserAddressSpaceStatus.InvalidEncoding;
            }
            for (uint j = 0; j < i; j++)
                if (Overlaps(range.VirtualStart, range.Pages * 4096, ranges[j].VirtualStart, ranges[j].Pages * 4096) ||
                    Overlaps(range.PhysicalStart, range.Pages * 4096, ranges[j].PhysicalStart, ranges[j].Pages * 4096))
                    return X64UserAddressSpaceStatus.OverlappingRanges;
            leaves += range.Pages;
            if (leaves > 32768)
                return X64UserAddressSpaceStatus.InvalidRange;
            uint first = (uint)(range.VirtualStart >> 21), last = (uint)((range.VirtualStart + range.Pages * 4096 - 1) >> 21);
            for (uint slot = first; slot <= last; slot++)
            {
                if (!slots[slot])
                {
                    slots[slot] = true;
                    required++;
                }
                if (!directories[slot / 512])
                {
                    directories[slot / 512] = true;
                    required++;
                }
            }
        }
        return required <= pages ? X64UserAddressSpaceStatus.Ready : X64UserAddressSpaceStatus.InsufficientTables;
    }

    [HostTest("batched leaf audit matches scalar audit for every lane corruption zero and allowed accessed-dirty bits")]
    private static void AuditLeafLanes()
    {
        var ranges = new[] { Mapping(0x3ffff000, 0x3000000, 20) };
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)Pages * 512).Clear();
            X64UserAddressSpaceResult result;
            fixed (X64UserMappingRange* r = ranges)
                result = X64UserAddressSpace.Build(r, 1, tables, Root, Pages, conditions);
            HostCheck.True(result.Succeeded);
            for (uint lane = 0; lane < 8; lane++)
            {
                ulong* leaf = Leaf(tables, 0x40000000 + lane * 4096);
                ulong saved = *leaf;
                foreach (ulong mask in new ulong[] { 0, 1, 2, 4, 0x80, 0x1000, 1UL << 63 })
                {
                    *leaf = mask == 0 ? 0 : saved ^ mask;
                    CheckScalarAudit(ranges, tables, conditions, result, false);
                    *leaf = saved;
                }
                foreach (ulong bits in new ulong[] { 0x20, 0x40, 0x60 })
                {
                    *leaf = saved | bits;
                    CheckScalarAudit(ranges, tables, conditions, result, true);
                    *leaf = saved;
                }
            }
            CheckScalarAudit(ranges, tables, conditions, result, true);
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("batched leaf audit preserves range ends PT boundaries holes and implicit table-arena leaves")]
    private static void AuditLeafBoundaries()
    {
        var readOnly = Mapping(0x600000, 0x6000000, 3);
        readOnly.Protection = PageProtection.Read;
        X64UserMappingRange[] ranges = [Mapping(0x80008000, 0x5008000, 9), Mapping(0x3ffff000, 0x4000000, 20),
            Mapping(0x402000, 0x3000000, 17), readOnly, Mapping(0x603000, 0x7000000, 9), Mapping(0x80000000, 0x5000000, 7)];
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)Pages * 512).Clear();
            X64UserAddressSpaceResult result;
            fixed (X64UserMappingRange* r = ranges)
                result = X64UserAddressSpace.Build(r, (uint)ranges.Length, tables, Root, Pages, conditions);
            HostCheck.True(result.Succeeded);
            CheckScalarAudit(ranges, tables, conditions, result, true);
            // Both sides of a PT/PDPT boundary, short range tails and the final
            // implicit arena must remain complete. Adjacent ranges differ in backing/protection.
            foreach (ulong va in new ulong[] { 0x3ffff000, 0x40000000, 0x40007000, 0x40008000, 0x40012000,
                0x412000, 0x602000, 0x603000, 0x80006000, 0x80010000, Root, Root + (Pages - 1) * 4096 })
            {
                ulong* leaf = Leaf(tables, va);
                ulong saved = *leaf;
                HostCheck.True(saved != 0);
                *leaf = 0;
                CheckScalarAudit(ranges, tables, conditions, result, false);
                *leaf = saved;
            }
            foreach (ulong va in new ulong[] { 0x401000, 0x413000, 0x80007000, 0x80011000 })
            {
                ulong* hole = Leaf(tables, va);
                HostCheck.Equal(0UL, *hole);
                *hole = *Leaf(tables, 0x402000);
                CheckScalarAudit(ranges, tables, conditions, result, false);
                *hole = 0;
            }
            CheckScalarAudit(ranges, tables, conditions, result, true);
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("batched leaf audit matches scalar exact leaf counts for short tails and adjacent unequal mappings")]
    private static void AuditLeafCounts()
    {
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            foreach (uint count in new uint[] { 1, 7, 8, 9, 15, 16, 17 })
            {
                X64UserMappingRange[] ranges = [Mapping(0x600000 + count * 4096, 0x4000000, 9), Mapping(0x600000, 0x3000000, count)];
                new Span<ulong>(tables, (int)Pages * 512).Clear();
                X64UserAddressSpaceResult result;
                fixed (X64UserMappingRange* r = ranges)
                    result = X64UserAddressSpace.Build(r, 2, tables, Root, Pages, conditions);
                HostCheck.True(result.Succeeded);
                HostCheck.Equal((ulong)count + 9 + Pages, result.LeafPages);
                CheckScalarAudit(ranges, tables, conditions, result, true);
                var wrong = result;
                wrong.LeafPages--;
                CheckScalarAudit(ranges, tables, conditions, wrong, false);
                wrong = result;
                wrong.LeafPages++;
                CheckScalarAudit(ranges, tables, conditions, wrong, false);
                wrong = result;
                wrong.UsedTables++;
                CheckScalarAudit(ranges, tables, conditions, wrong, false);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    private static void CheckScalarAudit(X64UserMappingRange[] ranges, ulong* tables,
        X64PageTableEncodingPreconditions conditions, X64UserAddressSpaceResult result, bool expected)
    {
        byte[] tableBytes = new ReadOnlySpan<byte>(tables, (int)Pages * 4096).ToArray();
        byte[] rangeBytes = MemoryMarshal.AsBytes(ranges.AsSpan()).ToArray();
        fixed (X64UserMappingRange* r = ranges)
        {
            bool scalar = ScalarAudit(r, (uint)ranges.Length, tables, conditions, result);
            HostCheck.Equal(expected, scalar);
            HostCheck.Equal(scalar, X64UserAddressSpace.Audit(r, (uint)ranges.Length, tables, Root, Pages, conditions, result));
        }
        HostCheck.True(tableBytes.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(tables, tableBytes.Length)));
        HostCheck.True(rangeBytes.AsSpan().SequenceEqual(MemoryMarshal.AsBytes(ranges.AsSpan())));
    }

    // Original one-leaf-at-a-time audit with an independent scalar admission
    // oracle. No production batching or leaf-cache helper is called here.
    private static bool ScalarAudit(X64UserMappingRange* ranges, uint count, ulong* tables,
        X64PageTableEncodingPreconditions conditions, X64UserAddressSpaceResult result)
    {
        if (ScalarAdmission(ranges, count, tables, Pages, conditions, out uint required, out ulong expectedLeaves) != X64UserAddressSpaceStatus.Ready ||
            !result.Succeeded || result.Root != Root || result.UsedTables != required || result.LeafPages != expectedLeaves)
            return false;
        bool[] seen = new bool[X64UserAddressSpace.MaximumTables];
        seen[0] = true;
        uint seenTables = 1;
        ulong leaves = 0, rangeStart = 0, rangeBytes = 0, rangeEntry = 0;
        for (uint ri = 0; ri < 512; ri++)
        {
            if (ri != 0)
            {
                if (tables[ri] != 0)
                    return false;
                continue;
            }
            if (!ScalarChild(tables[ri], X64PageTableLevel.Pml4e, conditions, seen, ref seenTables, out uint pdpt))
                return false;
            for (uint pi = 0; pi < 512; pi++)
            {
                ulong entry = tables[pdpt * 512 + pi];
                if (entry == 0)
                    continue;
                if (pi >= X64UserAddressSpace.UserEnd >> 30 || !ScalarChild(entry, X64PageTableLevel.Pdpte, conditions, seen, ref seenTables, out uint pd))
                    return false;
                for (uint di = 0; di < 512; di++)
                {
                    entry = tables[pd * 512 + di];
                    if (entry == 0)
                        continue;
                    if (!ScalarChild(entry, X64PageTableLevel.Pde, conditions, seen, ref seenTables, out uint pt))
                        return false;
                    for (uint ti = 0; ti < 512; ti++)
                    {
                        ulong leaf = tables[pt * 512 + ti];
                        if (leaf == 0)
                            continue;
                        ulong va = ((ulong)pi << 30) | ((ulong)di << 21) | ((ulong)ti << 12);
                        if (va < rangeStart || va - rangeStart >= rangeBytes)
                        {
                            bool matched = false;
                            for (uint r = 0; r <= count; r++)
                            {
                                var range = r == count ? new X64UserMappingRange
                                {
                                    VirtualStart = Root,
                                    PhysicalStart = Root,
                                    Pages = Pages,
                                    Protection = PageProtection.Read | PageProtection.Write,
                                    Cache = PageCachePolicy.WriteBack
                                } : ranges[r];
                                if (va < range.VirtualStart || va - range.VirtualStart >= range.Pages * 4096)
                                    continue;
                                if (PhysicalAddress.TryCreateFromHardwareRegisterBoundary(range.PhysicalStart, out var address) != AddressValueStatus.Success)
                                    return false;
                                var encoded = X64PageTableEntry.TryEncode(new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf, address, range.Protection, range.Cache), conditions);
                                if (!encoded.Succeeded)
                                    return false;
                                rangeEntry = encoded.Entry.RawValue | (range.User ? 4UL : 0UL);
                                rangeStart = range.VirtualStart;
                                rangeBytes = range.Pages * 4096;
                                matched = true;
                                break;
                            }
                            if (!matched)
                                return false;
                        }
                        if ((leaf & ~0x60UL) != rangeEntry + (va - rangeStart))
                            return false;
                        leaves++;
                    }
                }
            }
        }
        if (seenTables != required || leaves != expectedLeaves)
            return false;
        for (uint page = 1; page < Pages; page++)
            if (!seen[page])
                for (uint word = 0; word < 512; word++)
                    if (tables[page * 512 + word] != 0)
                        return false;
        return true;
    }

    private static bool ScalarChild(ulong raw, X64PageTableLevel level, X64PageTableEncodingPreconditions conditions,
        bool[] seen, ref uint count, out uint child)
    {
        child = 0;
        ulong addressValue = raw & 0x000ffffffffff000;
        if (addressValue <= Root || addressValue - Root >= Pages * 4096)
            return false;
        child = (uint)((addressValue - Root) / 4096);
        if (seen[child] || PhysicalAddress.TryCreateFromHardwareRegisterBoundary(addressValue, out var address) != AddressValueStatus.Success)
            return false;
        var encoded = X64PageTableEntry.TryEncode(new(level, X64PageTableEntryKind.TablePointer, address, PageProtection.None, PageCachePolicy.WriteBack), conditions);
        if (!encoded.Succeeded || (raw & ~0x20UL) != (encoded.Entry.RawValue | 4UL))
            return false;
        seen[child] = true;
        count++;
        return true;
    }

    [HostTest("socket roots retain high identity UC device leaves while user RAM and tables remain low", HostLane.Fast, 3)]
    private static void HighDevice()
    {
        const ulong device = 0x800004000;
        X64UserMappingRange mmio = new()
        {
            VirtualStart = device,
            PhysicalStart = device,
            Pages = 1,
            Protection = PageProtection.Read | PageProtection.Write,
            Cache = PageCachePolicy.Uncached
        };
        X64UserMappingRange[] ranges = [.. Ranges(), mmio];
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)Pages * 512).Clear();
            fixed (X64UserMappingRange* values = ranges)
            {
                var built = X64UserAddressSpace.Build(values, 5, tables, Root, Pages, Conditions());
                HostCheck.True(built.Succeeded && X64UserAddressSpace.Audit(values, 5, tables, Root, Pages, Conditions(), built));
                ulong entry = *Leaf(tables, device);
                HostCheck.True((entry & 4) == 0 && (entry & (1UL << 63)) != 0 && (entry & 0x000ffffffffff000) == device);
                foreach (int variant in Enumerable.Range(0, 6))
                {
                    new Span<ulong>(tables, (int)Pages * 512).Clear();
                    ranges[4] = mmio;
                    if (variant == 0)
                        ranges[4].User = true;
                    if (variant == 1)
                        ranges[4].Protection |= PageProtection.Execute;
                    if (variant == 2)
                        ranges[4].Cache = PageCachePolicy.WriteBack;
                    if (variant == 3)
                        ranges[4].VirtualStart += 4096;
                    if (variant == 4)
                        ranges[4].Pages = 2;
                    if (variant == 5)
                        ranges[4].VirtualStart = ranges[4].PhysicalStart = X64UserAddressSpace.UserEnd;
                    HostCheck.Equal(X64UserAddressSpaceStatus.InvalidRange, X64UserAddressSpace.Build(values, 5, tables, Root, Pages, Conditions()).Status);
                    HostCheck.True(new ReadOnlySpan<ulong>(tables, (int)Pages * 512).IndexOfAnyExcept(0UL) < 0);
                }
                ranges[4] = mmio;
                HostCheck.Equal(X64UserAddressSpaceStatus.InvalidInput, X64UserAddressSpace.Build(values, 5, tables, 0x100000000, Pages, Conditions()).Status);
                HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406, 0, 3, 255, out var pat));
                var narrow = X64PageTableEncodingPreconditions.CreateObserved(32, true, true, false, false, pat);
                HostCheck.True(!X64UserAddressSpace.Build(values, 5, tables, Root, Pages, narrow).Succeeded);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("m16-006 high user leaves cross PDPT boundaries while physical and supervisor bounds remain low", HostLane.Fast, 3)]
    private static void HighAddresses()
    {
        var ranges = Ranges();
        var conditions = Conditions();
        ranges[2].VirtualStart = 0x13ffff000; // Last page in PDPT[4], then PDPT[5].
        ranges[3].VirtualStart = X64UserAddressSpace.UserEnd - 4096;
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            fixed (X64UserMappingRange* r = ranges)
            {
                new Span<ulong>(tables, (int)Pages * 512).Clear();
                var result = X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions);
                HostCheck.True(result.Succeeded && X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                HostCheck.Equal(0x300005UL, *Leaf(tables, 0x13ffff000));
                HostCheck.Equal(0x301005UL, *Leaf(tables, 0x140000000));
                HostCheck.Equal(0x8000000000500007UL, *Leaf(tables, X64UserAddressSpace.UserEnd - 4096));
                HostCheck.True(X64UserAddressSpace.TryReadLeaf(tables, Root, Pages, 0x100000000, out ulong hole) && hole == 0);
                // A foreign high table pointer and an out-of-policy PDPT leaf
                // cannot be admitted by broadening user virtual bounds.
                uint pdpt = (uint)(((tables[0] & 0x000ffffffffff000) - Root) / 4096);
                tables[pdpt * 512 + 64] = tables[pdpt * 512 + 5];
                HostCheck.True(!X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                new Span<ulong>(tables, (int)Pages * 512).Clear();
                ranges[2].User = false;
                HostCheck.Equal(X64UserAddressSpaceStatus.InvalidRange, X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions).Status);
                ranges[2].User = true;
                ranges[2].PhysicalStart = 0x100000000;
                HostCheck.Equal(X64UserAddressSpaceStatus.InvalidRange, X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions).Status);
                HostCheck.True(!X64UserAddressSpace.TryReadLeaf(tables, 0x100000000, Pages, 0x400000, out _));
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("m16-004 independent user supervisor NX page encodings and absent guards", HostLane.Fast, 0)]
    private static void Encodings()
    {
        var ranges = Ranges();
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)Pages * 512).Clear();
            fixed (X64UserMappingRange* r = ranges)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var result = X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                HostCheck.True(result.Succeeded && result.Root == Root && result.UsedTables == 9 && result.LeafPages == 21 && allocated == 0);
                HostCheck.True(X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                HostCheck.Equal(0x100001UL, *Leaf(tables, 0x100000));
                HostCheck.Equal(0x8000000000200003UL, *Leaf(tables, 0x200000));
                HostCheck.Equal(0x300005UL, *Leaf(tables, 0x400000));
                HostCheck.Equal(0x301005UL, *Leaf(tables, 0x401000));
                HostCheck.Equal(0x8000000000500007UL, *Leaf(tables, 0x6ffff000));
                HostCheck.Equal(0x8000000001000003UL, *Leaf(tables, Root));
                foreach (ulong va in new ulong[] { 0, 0x402000, 0x6fffe000, 0x80000000 })
                    HostCheck.True(X64UserAddressSpace.TryReadLeaf(tables, Root, Pages, va, out ulong leaf) && leaf == 0);
                HostCheck.True(!X64UserAddressSpace.TryReadLeaf(tables, Root, Pages, X64UserAddressSpace.UserEnd, out _));
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("m16-004 malformed overlap W-X table exhaustion dirty arena reject before writes", HostLane.Fast, 1)]
    private static void Rejections()
    {
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            for (int variant = 0; variant < 8; variant++)
            {
                var ranges = Ranges();
                new Span<ulong>(tables, (int)Pages * 512).Clear();
                X64UserAddressSpaceStatus expected = X64UserAddressSpaceStatus.InvalidRange;
                if (variant == 0)
                    ranges[2].Pages = 0;
                if (variant == 1)
                    ranges[2].VirtualStart++;
                if (variant == 2)
                    ranges[2].PhysicalStart = 0xfffff000;
                if (variant == 3)
                {
                    ranges[2].PhysicalStart = ranges[0].PhysicalStart;
                    expected = X64UserAddressSpaceStatus.OverlappingRanges;
                }
                if (variant == 4)
                {
                    ranges[2].VirtualStart = Root;
                    expected = X64UserAddressSpaceStatus.OverlappingRanges;
                }
                if (variant == 5)
                {
                    ranges[2].Protection |= PageProtection.Write;
                    expected = X64UserAddressSpaceStatus.InvalidEncoding;
                }
                if (variant == 6)
                    expected = X64UserAddressSpaceStatus.InsufficientTables;
                if (variant == 7)
                {
                    tables[0] = 0xa5;
                    expected = X64UserAddressSpaceStatus.DirtyTables;
                }
                fixed (X64UserMappingRange* r = ranges)
                {
                    var result = X64UserAddressSpace.Build(r, 4, tables, Root, variant == 6 ? 4U : Pages, conditions);
                    HostCheck.Equal(expected, result.Status);
                    HostCheck.True(!result.Succeeded && result.Root == 0 && result.UsedTables == 0);
                    HostCheck.Equal(variant == 7 ? 0xa5UL : 0UL, tables[0]);
                    HostCheck.True(new ReadOnlySpan<ulong>(tables + 1, (int)Pages * 512 - 1).IndexOfAnyExcept(0UL) == -1);
                }
            }
            HostCheck.True(X64UserAddressSpace.Clear(tables, Pages));
            HostCheck.True(new ReadOnlySpan<ulong>(tables, (int)Pages * 512).IndexOfAnyExcept(0UL) == -1);
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("whole-root audit checks every lane of unused table boundary and interior groups without mutation or stale success")]
    private static void UnusedTableWords()
    {
        foreach (uint tablePages in new[] { Pages, X64UserAddressSpace.MaximumTables })
        {
            var ranges = Ranges();
            var conditions = Conditions();
            ulong* tables = (ulong*)NativeMemory.AlignedAlloc(tablePages * 4096, 4096);
            try
            {
                new Span<ulong>(tables, (int)tablePages * 512).Clear();
                fixed (X64UserMappingRange* r = ranges)
                {
                    var result = X64UserAddressSpace.Build(r, (uint)ranges.Length, tables, Root, tablePages, conditions);
                    HostCheck.True(result.Succeeded && X64UserAddressSpace.Audit(r, (uint)ranges.Length, tables, Root, tablePages, conditions, result));
                    // Derive unused pages from the actual graph, never from a
                    // presumed suffix beginning at the reported table count.
                    HashSet<uint> reachable = [0];
                    Queue<(uint Page, int Level)> pending = new();
                    pending.Enqueue((0, 3));
                    while (pending.TryDequeue(out var current))
                    {
                        if (current.Level == 0)
                            continue;
                        for (uint i = 0; i < 512; i++)
                        {
                            ulong entry = tables[current.Page * 512 + i];
                            if (entry == 0)
                                continue;
                            ulong physical = entry & 0x000ffffffffff000;
                            HostCheck.True(physical >= Root && physical - Root < tablePages * 4096UL);
                            uint child = (uint)((physical - Root) / 4096);
                            HostCheck.True(reachable.Add(child));
                            pending.Enqueue((child, current.Level - 1));
                        }
                    }
                    HostCheck.Equal(result.UsedTables, (uint)reachable.Count);
                    uint[] unused = Enumerable.Range(1, (int)tablePages - 1).Select(i => (uint)i).Where(i => !reachable.Contains(i)).ToArray();
                    HostCheck.True(unused.Length >= 2);
                    int[] words = Enumerable.Range(0, 8).Concat(new[] { 8 }).Concat(Enumerable.Range(248, 8)).Concat(Enumerable.Range(504, 8)).ToArray();
                    byte[] original = new ReadOnlySpan<byte>(tables, (int)tablePages * 4096).ToArray();
                    byte[] originalRanges = MemoryMarshal.AsBytes(ranges.AsSpan()).ToArray();
                    foreach (uint page in new[] { unused[0], unused[^1] })
                        foreach (int word in words)
                            foreach (ulong value in new ulong[] { 1, 0x60, 1UL << 63 })
                            {
                                ulong* changed = tables + page * 512 + word;
                                HostCheck.Equal(0UL, *changed);
                                *changed = value;
                                byte[] before = new ReadOnlySpan<byte>(tables, (int)tablePages * 4096).ToArray();
                                try
                                {
                                    HostCheck.True(!X64UserAddressSpace.Audit(r, (uint)ranges.Length, tables, Root, tablePages, conditions, result));
                                    HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(tables, before.Length)));
                                    HostCheck.True(originalRanges.AsSpan().SequenceEqual(MemoryMarshal.AsBytes(ranges.AsSpan())));
                                }
                                finally
                                {
                                    *changed = 0;
                                }
                                HostCheck.True(original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(tables, original.Length)));
                                HostCheck.True(X64UserAddressSpace.Audit(r, (uint)ranges.Length, tables, Root, tablePages, conditions, result));
                            }
                }
            }
            finally
            {
                NativeMemory.AlignedFree(tables);
            }
        }
    }

    [HostTest("m16-006 audit checks every leaf across unsorted long ranges and the physical limit without retaining stale encodings", HostLane.Fast, 4)]
    private static void LongRangeAudit()
    {
        const uint tablePages = 32, leafPages = 520;
        const ulong high = 0x23ffff000;
        var basic = Ranges();
        var conditions = Conditions();
        X64UserMappingRange large = new()
        {
            VirtualStart = high,
            PhysicalStart = X64UserAddressSpace.PhysicalEnd - leafPages * 4096UL,
            Pages = leafPages,
            Protection = PageProtection.Read | PageProtection.Write,
            Cache = PageCachePolicy.WriteBack,
            User = true
        };
        X64UserMappingRange[] ranges = [basic[3], large, basic[1], basic[2], basic[0]];
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(tablePages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)tablePages * 512).Clear();
            fixed (X64UserMappingRange* r = ranges)
            {
                var result = X64UserAddressSpace.Build(r, 5, tables, Root, tablePages, conditions);
                HostCheck.True(result.Succeeded);
                HostCheck.Equal(0x80000000fffff007UL, *Leaf(tables, high + (leafPages - 1) * 4096UL));
                *Leaf(tables, high + 511 * 4096UL) |= 0x60;
                HostCheck.True(X64UserAddressSpace.Audit(r, 5, tables, Root, tablePages, conditions, result));
                byte[] original = new ReadOnlySpan<byte>(tables, (int)tablePages * 4096).ToArray();
                foreach (uint page in new uint[] { 0, 1, 255, 256, 511, 519 })
                    foreach (ulong mask in new ulong[] { 4, 0x1000, 0x8000000000000000 })
                    {
                        ulong* leaf = Leaf(tables, high + page * 4096UL);
                        ulong saved = *leaf;
                        *leaf ^= mask;
                        ulong changed = *leaf;
                        HostCheck.True(!X64UserAddressSpace.Audit(r, 5, tables, Root, tablePages, conditions, result));
                        HostCheck.Equal(changed, *leaf);
                        *leaf = saved;
                    }
                ulong* extra = Leaf(tables, high + leafPages * 4096UL);
                *extra = *Leaf(tables, high);
                HostCheck.True(!X64UserAddressSpace.Audit(r, 5, tables, Root, tablePages, conditions, result));
                *extra = 0;
                HostCheck.True(original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(tables, (int)tablePages * 4096)));
                // A later call must derive its encoding from the current ranges.
                r[1].Protection = PageProtection.Read;
                HostCheck.True(!X64UserAddressSpace.Audit(r, 5, tables, Root, tablePages, conditions, result));
                r[1].Protection = PageProtection.Read | PageProtection.Write;
                HostCheck.True(X64UserAddressSpace.Audit(r, 5, tables, Root, tablePages, conditions, result));
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("m16-006 contiguous leaf encoding preserves selected PAT cache bits independently of A-D", HostLane.Fast, 5)]
    private static void RangeCacheBits()
    {
        HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0107040600070406, 0, 3, 7, out var pat));
        var conditions = X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat);
        PageCachePolicy[] policies = [PageCachePolicy.WriteBack, PageCachePolicy.Uncached, PageCachePolicy.WriteCombining];
        ulong[] cacheBits = [0, 0x18, 0x98];
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            for (int i = 0; i < policies.Length; i++)
            {
                var ranges = Ranges();
                ranges[2].Protection = PageProtection.Read | PageProtection.Write;
                ranges[2].Cache = policies[i];
                new Span<ulong>(tables, (int)Pages * 512).Clear();
                fixed (X64UserMappingRange* r = ranges)
                {
                    var result = X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions);
                    HostCheck.True(result.Succeeded);
                    HostCheck.Equal(0x8000000000300007UL | cacheBits[i], *Leaf(tables, 0x400000));
                    HostCheck.Equal(0x8000000000301007UL | cacheBits[i], *Leaf(tables, 0x401000));
                    *Leaf(tables, 0x401000) |= 0x60;
                    HostCheck.True(X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                    *Leaf(tables, 0x401000) ^= 0x80;
                    HostCheck.True(!X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                }
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    [HostTest("m16-004 whole-root audit rejects forged permissions aliases cycles extra leaves and unused tables", HostLane.Fast, 2)]
    private static void AuditMutations()
    {
        var ranges = Ranges();
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            fixed (X64UserMappingRange* r = ranges)
            {
                for (int variant = 0; variant < 7; variant++)
                {
                    new Span<ulong>(tables, (int)Pages * 512).Clear();
                    var result = X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions);
                    HostCheck.True(result.Succeeded);
                    if (variant == 0)
                        *Leaf(tables, 0x400000) &= ~4UL;
                    if (variant == 1)
                        *Leaf(tables, 0x100000) |= 4;
                    if (variant == 2)
                        *Leaf(tables, 0x6ffff000) &= ~0x8000000000000000UL;
                    if (variant == 3)
                        *Leaf(tables, 0x400000) |= 2;
                    if (variant == 4)
                        *Leaf(tables, 0x402000) = 0x300005;
                    if (variant == 5)
                        tables[0] = Root | 7;
                    if (variant == 6)
                        tables[(Pages - 1) * 512] = 1;
                    HostCheck.True(!X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                }
            }
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }
}
