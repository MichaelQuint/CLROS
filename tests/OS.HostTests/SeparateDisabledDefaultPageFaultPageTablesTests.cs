using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;
using Status = OS.Kernel.Diagnostics.SeparatePageFaultPageTableStatus;

internal static unsafe class SeparateDisabledDefaultPageFaultPageTablesTests
{
    private const ulong Page = 4096;
    private const ulong PhysicalMask = 0x000f_ffff_ffff_f000;
    private const ulong Nx = 1UL << 63;
    private const PageProtection Rx = PageProtection.Read | PageProtection.Execute;
    private const PageProtection Rw = PageProtection.Read | PageProtection.Write;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.014.page_image");
        try
        {
            TestDefaultsAndBound();
            TestIndependentPositiveWalks();
            TestInvalidRanges();
            TestModeAndArenaBoundaries();
            TestStorageBoundaries();
            TestImageCorruption();
            TestMetadataCorruptionAndSnapshotBoundary();
            TestAllocationAndSourceBoundary();
        }
        catch (Exception exception)
        {
            Console.WriteLine("TEST_FAIL m02.014.page_image " + exception.Message);
            throw;
        }
        Console.WriteLine("CLROS_M02_014_PAGE_IMAGE_HOST_READY maxRanges=8 maxRangePages=16 " +
            "maxTablePages=55 suppliedImageAudited=true guestExecution=false cr3Installed=false " +
            "activePermissionsObserved=false tlbInvalidated=false faultTriggered=false");
        Console.WriteLine("TEST_PASS m02.014.page_image");
    }

    private static void TestDefaultsAndBound()
    {
        SeparatePageFaultPageTableResult empty = default;
        Require(!empty.IsInitialized && !empty.Succeeded && !empty.SuppliedImageAudited, "default result");
        NoHardware(empty);
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultMappingRange>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultPageTableRequest>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultPageTableResult>(), "unmanaged values");
        Require(Image.RequiredTablePageBound(0) == 0 && Image.RequiredTablePageBound(9) == 0 &&
            Image.RequiredTablePageBound(ulong.MaxValue) == 0, "invalid count bound");
        Require(Image.MaximumRangePages * Page < 2UL * 1024 * 1024 &&
            Image.MaximumTablePages * Page < 2UL * 1024 * 1024, "both interval bounds below 2 MiB");
        for (ulong ranges = 1; ranges <= 8; ranges++)
            Require(Image.RequiredTablePageBound(ranges) == 1 + 6 * (ranges + 1), "conservative bound formula");
        using Fixture fixture = new();
        fixture.Request = default;
        RejectUnchanged(fixture, Status.InvalidRequest);
        fixture.Request = Request();
        SeparatePageFaultPageTableResult result = fixture.Build();
        Require(result.Succeeded, "baseline");
        foreach (string field in new[] { "_initialized", "<Status>k__BackingField" })
        {
            SeparatePageFaultPageTableResult corrupt = result;
            Bytes(ref corrupt)[Offset<SeparatePageFaultPageTableResult>(field)] = 0xff;
            Require(!corrupt.Succeeded, "corrupt result " + field);
        }
        foreach (string field in new[] { "<UsedTablePages>k__BackingField", "<MappedPages>k__BackingField",
            "<RangeCount>k__BackingField" })
        {
            SeparatePageFaultPageTableResult corrupt = result;
            Bytes(ref corrupt).Slice(Offset<SeparatePageFaultPageTableResult>(field), 8).Clear();
            Require(!corrupt.Succeeded, "zero result field " + field);
        }
        SeparatePageFaultPageTableResult inconsistent = result;
        int arenaCount = Offset<SeparatePageFaultPageTableResult>("<Request>k__BackingField") +
            Offset<SeparatePageFaultPageTableRequest>("<ArenaPageCount>k__BackingField");
        Bytes(ref inconsistent)[arenaCount]++;
        Require(!inconsistent.Succeeded, "result request and absence counts must agree");
    }

    private static void TestIndependentPositiveWalks()
    {
        using (Fixture fixture = new())
        {
            // Trailing capacity is caller storage, not part of the requested image.
            ulong end = fixture.Request.ArenaPageCount * 512;
            fixture.Entries[end] = 0xfeed_1234;
            byte* metadataTail = (byte*)(fixture.Pages + fixture.Request.ArenaPageCount);
            metadataTail[0] = 0x5a;
            VerifyImage(fixture);
            Require(fixture.Entries[end] == 0xfeed_1234 && metadataTail[0] == 0x5a, "capacity tail unchanged");
        }
        foreach (ulong boundary in new[] { 1UL << 21, 1UL << 30, 1UL << 39 })
        {
            using Fixture fixture = new();
            fixture.Ranges[0] = new(boundary - Page, 2, Rx);
            VerifyImage(fixture);
            using Fixture arenaCrossing = new();
            arenaCrossing.Request = Request(boundary - Page);
            VerifyImage(arenaCrossing);
        }
        using (Fixture maximum = new())
        {
            maximum.RangeCount = 8;
            maximum.Request = Request(0x10_0000, 55);
            for (ulong index = 0; index < 8; index++)
                maximum.Ranges[index] = new((index + 1) * (1UL << 39) - Page, 16, index % 2 == 0 ? Rx : Rw);
            VerifyImage(maximum);
        }
        using (Fixture high = new())
        {
            high.Ranges[0] = new(0x0000_8000_0000_0000 - 16 * Page, 16, Rx);
            VerifyImage(high);
        }
        using (Fixture narrow = new())
        {
            narrow.Request = Request(mode: Mode(width: 36));
            narrow.Ranges[0] = new((1UL << 36) - Page, 1, Rx);
            VerifyImage(narrow);
        }
        using (Fixture shuffled = new())
        {
            (shuffled.Ranges[0], shuffled.Ranges[1]) = (shuffled.Ranges[1], shuffled.Ranges[0]);
            VerifyImage(shuffled);
        }
    }

    private static void TestInvalidRanges()
    {
        SeparatePageFaultMappingRange[] bad =
        [
            default, new(0, 1, Rx), new(Page - 1, 1, Rx), new(0x40_0001, 1, Rx),
            new(0x40_0000, 0, Rx), new(0x40_0000, 17, Rx), new(0x40_0000, ulong.MaxValue, Rx),
            new(ulong.MaxValue - Page + 1, 2, Rx), new(0x0000_8000_0000_0000, 1, Rx),
            new(0xffff_8000_0000_0000, 1, Rx), new(0x0000_8000_0000_0000 - Page, 2, Rx),
            new(0x40_0000, 1, PageProtection.None), new(0x40_0000, 1, PageProtection.Read),
            new(0x40_0000, 1, PageProtection.Write), new(0x40_0000, 1, PageProtection.Execute),
            new(0x40_0000, 1, Rx | PageProtection.Write), new(0x40_0000, 1, (PageProtection)255),
            new(0x40_0000, 1, Rx, PageCachePolicy.Uncached),
            new(0x40_0000, 1, Rx, PageCachePolicy.WriteCombining),
            new(0x40_0000, 1, Rx, PageCachePolicy.Uninitialized), new(0x40_0000, 1, Rx, (PageCachePolicy)255)
        ];
        foreach (SeparatePageFaultMappingRange range in bad)
        {
            using Fixture fixture = new();
            fixture.Ranges[0] = range;
            RejectUnchanged(fixture, Status.InvalidRange);
        }
        using (Fixture fixture = new())
        {
            fixture.Ranges[0] = fixture.Ranges[1];
            RejectUnchanged(fixture, Status.RangeOverlap);
            fixture.Ranges[0] = new(fixture.Ranges[1].Start + Page, 1, Rx);
            RejectUnchanged(fixture, Status.RangeOverlap);
            fixture.Ranges[0] = new(0x10_0000, 1, Rx);
            RejectUnchanged(fixture, Status.TableArenaOverlap);
            fixture.Ranges[0] = new(0x10_0000 - Page, 2, Rw);
            RejectUnchanged(fixture, Status.TableArenaOverlap);
        }
        using (Fixture fixture = new())
        {
            ref SeparatePageFaultMappingRange range = ref fixture.Ranges[0];
            Bytes(ref range)[Offset<SeparatePageFaultMappingRange>("_initialized")] = 2;
            RejectUnchanged(fixture, Status.InvalidRange);
        }
        foreach (ulong count in new[] { 0UL, 9UL, ulong.MaxValue })
        {
            using Fixture fixture = new();
            fixture.RangeCount = count;
            RejectUnchanged(fixture, Status.InvalidRangeCount);
        }
    }

    private static void TestModeAndArenaBoundaries()
    {
        foreach (ulong start in new[] { 0UL, 1UL, 0x10_0001UL, 0x0000_8000_0000_0000UL,
            0x0000_8000_0000_0000UL - Page, ulong.MaxValue - Page + 1 })
        {
            using Fixture fixture = new();
            fixture.Request = Request(start);
            RejectUnchanged(fixture, Status.InvalidRequest);
        }
        foreach (ulong count in new[] { 0UL, 56UL, ulong.MaxValue })
        {
            using Fixture fixture = new();
            fixture.Request = Request(pages: count);
            RejectUnchanged(fixture, Status.InvalidRequest);
        }
        for (ulong count = 1; count < 19; count++)
        {
            using Fixture fixture = new();
            fixture.Request = Request(pages: count);
            RejectUnchanged(fixture, Status.InsufficientTablePages);
        }
        X64PageTableEncodingPreconditions[] modes =
        [
            default, Mode(width: 31), Mode(width: 53), Mode(nx: false), Mode(wp: false), Mode(la57: true),
            X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, default),
            Mode(pat: 0x0006_0501_0007_0400, wb: 3, uc: 0), // PAT index zero is not WB.
        ];
        foreach (X64PageTableEncodingPreconditions mode in modes)
        {
            using Fixture fixture = new();
            fixture.Request = Request(mode: mode);
            RejectUnchanged(fixture, Status.InvalidPreconditions);
        }
        using (Fixture fixture = new())
        {
            SeparatePageFaultPageTableRequest request = fixture.Request;
            Bytes(ref request)[Offset<SeparatePageFaultPageTableRequest>("_initialized")] = 255;
            fixture.Request = request;
            RejectUnchanged(fixture, Status.InvalidRequest);
        }
        using (Fixture fixture = new())
        {
            fixture.Request = Request(mode: Mode(width: 36));
            fixture.Ranges[0] = new(1UL << 36, 1, Rx);
            RejectUnchanged(fixture, Status.InvalidRange);
            fixture.Ranges[0] = new((1UL << 36) - Page, 2, Rx);
            RejectUnchanged(fixture, Status.InvalidRange);
            fixture.Request = Request((1UL << 36) - Page, mode: Mode(width: 36));
            RejectUnchanged(fixture, Status.InvalidRequest);
        }
    }

    private static void TestStorageBoundaries()
    {
        using Fixture fixture = new();
        byte[] pagesBefore = fixture.PageBytes.ToArray();
        byte[] entriesBefore = fixture.EntryBytes.ToArray();
        for (int test = 0; test < 18; test++)
        {
            SeparatePageFaultMappingRange* ranges = fixture.Ranges;
            X64PageTableImagePage* pages = fixture.Pages;
            ulong* entries = fixture.Entries;
            ulong rangeCapacity = 8, pageCapacity = 55, entryCapacity = Image.MaximumEntries;
            Status expected = Status.InvalidStorage;
            switch (test)
            {
                case 0: ranges = null; break;
                case 1: pages = null; break;
                case 2: entries = null; break;
                case 3: ranges = (SeparatePageFaultMappingRange*)((byte*)ranges + 1); break;
                case 4: pages = (X64PageTableImagePage*)((byte*)pages + 1); break;
                case 5: entries = (ulong*)((byte*)entries + 1); break;
                case 6: rangeCapacity = 1; expected = Status.InvalidRangeCount; break;
                case 7: rangeCapacity = ulong.MaxValue; expected = Status.InvalidRangeCount; break;
                case 8: pageCapacity = 18; break;
                case 9: pageCapacity = ulong.MaxValue; break;
                case 10: entryCapacity = 19 * 512 - 1; break;
                case 11: entryCapacity = ulong.MaxValue; break;
                case 12: pages = (X64PageTableImagePage*)ranges; expected = Status.StorageOverlap; break;
                case 13: entries = (ulong*)ranges; expected = Status.StorageOverlap; break;
                case 14: entries = (ulong*)pages; expected = Status.StorageOverlap; break;
                case 15: ranges = (SeparatePageFaultMappingRange*)(ulong.MaxValue - 7); break;
                case 16: pages = (X64PageTableImagePage*)(ulong.MaxValue - 7); break;
                case 17: entries = (ulong*)(ulong.MaxValue - 7); break;
            }
            SeparatePageFaultPageTableResult result = Image.Build(fixture.Request, ranges, 2,
                rangeCapacity, pages, pageCapacity, entries, entryCapacity);
            Require(result.Status == expected && !result.Succeeded, "storage rejection " + test + ": " + result.Status);
            Require(pagesBefore.AsSpan().SequenceEqual(fixture.PageBytes) &&
                entriesBefore.AsSpan().SequenceEqual(fixture.EntryBytes), "storage rejection wrote output");
            result = Image.Audit(fixture.Request, ranges, 2, rangeCapacity, pages, pageCapacity, entries, entryCapacity);
            Require(result.Status == expected && !result.Succeeded, "audit storage rejection " + test);
        }
        fixture.Entries[0] = 1;
        RejectUnchanged(fixture, Status.StorageNotZero);
        fixture.Entries[0] = 0;
        fixture.Entries[19 * 512 - 1] = 1;
        RejectUnchanged(fixture, Status.StorageNotZero);
    }

    private static void TestImageCorruption()
    {
        Corrupt(f => f.Entries[0] = 0, Status.MissingMapping, "missing subtree");
        Corrupt(f => f.Entries[0] = 0x9000_0003, Status.ForeignTableTarget, "foreign target");
        Corrupt(f => f.Entries[0] = 0x10_0003, Status.RepeatedTableTarget, "root cycle");
        Corrupt(f => f.Entries[1] = f.Entries[0], Status.RepeatedTableTarget, "duplicate table role");
        Corrupt(f => f.Entries[0] |= 1UL << 62, Status.MalformedEntry, "reserved parent bit");
        Corrupt(f => f.Entries[0] |= 0x80, Status.MalformedEntry, "PML4 huge flag");
        Corrupt(f => f.Entries[0] |= 4, Status.MalformedEntry, "user parent");
        Corrupt(f => f.Entries[0] |= 0x20, Status.MalformedEntry, "not pristine accessed parent");
        Corrupt(f => f.Entries[2] = 2, Status.MalformedEntry, "nonzero absent entry");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] = 0, Status.MissingMapping, "missing leaf");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] |= 2, Status.MalformedEntry, "write executable leaf");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] |= 4, Status.MalformedEntry, "user leaf");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] |= Nx, Status.MappingMismatch, "NX code");
        Corrupt(f => f.Entries[FindEntry(f, 0x80_0000, 1)] &= ~Nx, Status.MalformedEntry, "executable data");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] += Page, Status.MappingMismatch, "nonidentity physical alias");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] |= 0x40, Status.MappingMismatch, "dirty leaf");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] |= 1UL << 50, Status.MalformedEntry, "physical width bit");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1)] |= 1UL << 59, Status.MalformedEntry, "protection key");
        Corrupt(f => f.Entries[FindEntry(f, 0x40_0000, 1) + 2] = 0x40_2001,
            Status.UnexpectedMapping, "undeclared leaf");
        Corrupt(f => f.Entries[(19 - 1) * 512] = 1, Status.UnusedTableNotZero, "unused page content");
        Corrupt(f => f.Entries[1] = 0x11_2003, Status.UnexpectedMapping, "empty surplus subtree");
        Corrupt(f => f.Entries[FindEntry(f, 0, 3)] = 0x4000_0081, Status.MalformedEntry, "1 GiB zero mapping");
        Corrupt(f => f.Entries[FindEntry(f, 0, 2)] = 0x20_0081, Status.MalformedEntry, "2 MiB zero mapping");
        Corrupt(f => f.Entries[FindEntry(f, 0, 1)] = 1, Status.MalformedEntry, "zero physical leaf");
        Corrupt(f => f.Entries[FindEntry(f, 0, 1)] = 0x1001, Status.UnexpectedMapping, "4 KiB zero mapping");
    }

    private static void TestMetadataCorruptionAndSnapshotBoundary()
    {
        using Fixture fixture = new();
        SeparatePageFaultPageTableResult initial = fixture.Build();
        Require(initial.Succeeded, "metadata baseline");
        for (int page = 0; page < 19; page++)
        {
            X64PageTableImagePage original = fixture.Pages[page];
            fixture.Pages[page] = default;
            Require(fixture.Audit().Status == Status.InvalidImageMetadata, "default metadata " + page);
            fixture.Pages[page] = original;
            if (page != 0)
            {
                fixture.Pages[page] = fixture.Pages[0];
                Require(fixture.Audit().Status == Status.InvalidImageMetadata, "duplicate metadata " + page);
                fixture.Pages[page] = original;
            }
        }
        ref X64PageTableImagePage record = ref fixture.Pages[18];
        byte[] bytes = Bytes(ref record).ToArray();
        for (int index = 0; index < bytes.Length; index++)
        {
            Bytes(ref record)[index] ^= 0xff;
            // Padding has no semantic meaning; all represented fields must be checked.
            if (index < 8 || index >= Offset<X64PageTableImagePage>("_physicalStart"))
                Require(fixture.Audit().Status == Status.InvalidImageMetadata, "metadata byte " + index);
            bytes.CopyTo(Bytes(ref record));
        }
        fixture.Ranges[0] = new(0x40_0000, 1, Rx);
        Require(!fixture.Audit().Succeeded, "stale range metadata");
        Require(initial.Succeeded && !initial.Cr3Installed, "result is a non-owning snapshot, not revocable lifetime authority");
        fixture.Ranges[0] = new(0x40_0000, 2, Rx);
        fixture.Request = Request(0x20_0000);
        Require(fixture.Audit().Status == Status.InvalidImageMetadata, "stale root metadata");
    }

    private static void TestAllocationAndSourceBoundary()
    {
        using Fixture fixture = new();
        ReadOnlySpan<byte> inputs = new(fixture.Ranges, 8 * sizeof(SeparatePageFaultMappingRange));
        byte[] inputSnapshot = inputs.ToArray();
        for (int repeat = 0; repeat < 12; repeat++)
        {
            fixture.EntryBytes.Clear();
            Require(fixture.Build().Succeeded && fixture.Audit().Succeeded, "warmup");
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool passed = true;
        for (int repeat = 0; repeat < 64; repeat++)
        {
            fixture.EntryBytes.Clear();
            passed &= fixture.Build().Succeeded;
            passed &= fixture.Audit().Succeeded;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(passed && allocated == 0, "zero allocation; bytes=" + allocated);
        Require(inputSnapshot.AsSpan().SequenceEqual(inputs), "caller ranges unchanged");
        byte[] snapshot = fixture.EntryBytes.ToArray();
        Require(fixture.Audit().Succeeded && snapshot.AsSpan().SequenceEqual(fixture.EntryBytes), "read-only audit");
        string root = FindRoot();
        string source = File.ReadAllText(Path.Combine(root,
            "src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultPageTables.cs"));
        foreach (string forbidden in new[] { "using System", "Probes.", "NullPageFaultProbePlan",
            "SeparateDisabledDefaultPageFaultCR2ErrorCode", "delegate*", "throw ", "DllImport", "Marshal.", "stackalloc" })
            Require(!source.Contains(forbidden, StringComparison.Ordinal), "source boundary " + forbidden);
        Require(source.Contains("X64PageTableEntry.TryEncode", StringComparison.Ordinal) &&
            source.Contains("X64PageTableEntry.TryDecode", StringComparison.Ordinal) &&
            source.Contains("PageZeroMappingPolicy.AuditTableImage", StringComparison.Ordinal), "actual entry/absence dependencies");
        fixture.AssertCanaries();
    }

    private static void VerifyImage(Fixture fixture)
    {
        SeparatePageFaultPageTableResult result = fixture.Build();
        Require(result.Succeeded && result.SuppliedImageAudited, "valid build: " + result.Status);
        Require(result.UsedTablePages <= result.ConservativeTablePageBound &&
            result.Request.ArenaPageCount >= result.ConservativeTablePageBound, "table bound");
        NoHardware(result);
        Dictionary<ulong, ulong> expected = new();
        for (ulong range = 0; range < fixture.RangeCount; range++)
            for (ulong page = 0; page < fixture.Ranges[range].PageCount; page++)
            {
                ulong address = fixture.Ranges[range].Start + page * Page;
                expected.Add(address, address | (fixture.Ranges[range].Protection == Rx ? 1UL : Nx | 3));
            }
        ulong root = Raw(fixture.Request.ArenaStart);
        for (ulong page = 0; page < fixture.Request.ArenaPageCount; page++)
        {
            ulong address = root + page * Page;
            expected.Add(address, address | Nx | 3);
            Require(Raw(fixture.Pages[page].PhysicalStart) == address &&
                fixture.Pages[page].EntryStartIndex == page * 512, "independent metadata");
        }
        HashSet<ulong> visited = new();
        ulong leaves = WalkIndependent(fixture, 0, 4, 0, expected, visited);
        Require(expected.Count == 0 && leaves == result.MappedPages && (ulong)visited.Count == result.UsedTablePages,
            "independent complete exact coverage");
        for (ulong page = 0; page < fixture.Request.ArenaPageCount; page++)
            if (!visited.Contains(page))
                for (ulong index = 0; index < 512; index++)
                    Require(fixture.Entries[page * 512 + index] == 0, "independent unused page zero");
        ulong table = 0;
        bool absent = false;
        for (int level = 4; level >= 1; level--)
        {
            ulong entry = fixture.Entries[table * 512];
            if ((entry & 1) == 0) { absent = true; break; }
            Require(level > 1 && (entry & 0x80) == 0, "zero must not have leaf");
            table = ((entry & PhysicalMask) - root) / Page;
        }
        Require(absent && result.PageZero.ProvesPageZeroAbsentInSuppliedImage && fixture.Audit().Succeeded,
            "independent zero walk");
        fixture.AssertCanaries();
    }

    // This oracle uses architectural raw bits and a test-owned expected map;
    // it does not call the production encoder, decoder, builder walk or policy audit.
    private static ulong WalkIndependent(Fixture fixture, ulong table, int level, ulong prefix,
        Dictionary<ulong, ulong> expected, HashSet<ulong> visited)
    {
        Require(table < fixture.Request.ArenaPageCount && visited.Add(table), "oracle distinct owned table");
        ulong leaves = 0;
        for (ulong index = 0; index < 512; index++)
        {
            ulong raw = fixture.Entries[table * 512 + index];
            if (raw == 0) continue;
            ulong address = prefix | (index << (12 + 9 * (level - 1)));
            if (level == 1)
            {
                Require(expected.Remove(address, out ulong wanted) && wanted == raw, "oracle leaf value");
                leaves++;
            }
            else
            {
                Require((raw & ~PhysicalMask) == 3, "oracle supervisor WB table");
                ulong physical = raw & PhysicalMask;
                ulong root = Raw(fixture.Request.ArenaStart);
                Require(physical >= root && physical - root < fixture.Request.ArenaPageCount * Page,
                    "oracle table target bounds");
                leaves += WalkIndependent(fixture, (physical - root) / Page, level - 1, address, expected, visited);
            }
        }
        return leaves;
    }

    private static void Corrupt(Action<Fixture> corrupt, Status expected, string name)
    {
        using Fixture fixture = new();
        Require(fixture.Build().Succeeded, "corrupt baseline");
        corrupt(fixture);
        byte[] entries = fixture.EntryBytes.ToArray();
        byte[] pages = fixture.PageBytes.ToArray();
        SeparatePageFaultPageTableResult result = fixture.Audit();
        Require(!result.Succeeded && result.Status == expected,
            name + " expected=" + expected + " actual=" + result.Status);
        Require(entries.AsSpan().SequenceEqual(fixture.EntryBytes) && pages.AsSpan().SequenceEqual(fixture.PageBytes),
            "corrupt audit mutated image");
        fixture.AssertCanaries();
    }

    private static ulong FindEntry(Fixture fixture, ulong address, int wantedLevel)
    {
        ulong table = 0;
        for (int level = 4; level >= wantedLevel; level--)
        {
            ulong index = table * 512 + ((address >> (12 + 9 * (level - 1))) & 511);
            if (level == wantedLevel) return index;
            table = ((fixture.Entries[index] & PhysicalMask) - Raw(fixture.Request.ArenaStart)) / Page;
        }
        throw new InvalidOperationException("invalid oracle level");
    }

    private static void RejectUnchanged(Fixture fixture, Status expected)
    {
        byte[] pages = fixture.PageBytes.ToArray();
        byte[] entries = fixture.EntryBytes.ToArray();
        SeparatePageFaultPageTableResult result = fixture.Build();
        Require(!result.Succeeded && result.Status == expected, "rejection expected=" + expected + " actual=" + result.Status);
        Require(pages.AsSpan().SequenceEqual(fixture.PageBytes) && entries.AsSpan().SequenceEqual(fixture.EntryBytes),
            "preflight rejection mutated storage");
        NoHardware(result);
        fixture.AssertCanaries();
    }

    private static void NoHardware(SeparatePageFaultPageTableResult result) => Require(!result.IsGuestEvidence &&
        !result.Cr3Installed && !result.ActivePermissionsObserved && !result.TlbInvalidated && !result.FaultTriggered,
        "host evidence boundary");

    private static SeparatePageFaultPageTableRequest Request(ulong start = 0x10_0000, ulong pages = 19,
        X64PageTableEncodingPreconditions? mode = null) => new(Address(start), pages, mode ?? Mode());

    private static X64PageTableEncodingPreconditions Mode(byte width = 48, bool nx = true, bool wp = true,
        bool la57 = false, ulong pat = 0x0006_0501_0007_0406, byte wb = 0, byte uc = 3)
    {
        X64PatMappingToken.TryCreate(pat, wb, uc, 4, out X64PatMappingToken mapping);
        return X64PageTableEncodingPreconditions.CreateObserved(width, nx, wp, la57, false, mapping);
    }

    private static PhysicalAddress Address(ulong raw)
    {
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(raw, out PhysicalAddress address);
        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        address.TryGetRawValueForHardwareRegisterBoundary(out ulong raw);
        return raw;
    }

    private static Span<byte> Bytes<T>(ref T value) where T : unmanaged => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));
    private static int Offset<T>(string field) where T : unmanaged => checked((int)Marshal.OffsetOf<T>(field));
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CLROS.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("repository root unavailable");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly byte* _pageAllocation;
        private readonly byte* _entryAllocation;
        private const ulong Canary = 0xfeed_1234_dead_5678;
        internal readonly SeparatePageFaultMappingRange* Ranges;
        internal readonly X64PageTableImagePage* Pages;
        internal readonly ulong* Entries;
        internal ulong RangeCount = 2;
        internal SeparatePageFaultPageTableRequest Request = SeparateDisabledDefaultPageFaultPageTablesTests.Request();
        internal Span<byte> PageBytes => new(Pages, checked(55 * sizeof(X64PageTableImagePage)));
        internal Span<byte> EntryBytes => new(Entries, checked((int)Image.MaximumEntries * sizeof(ulong)));

        internal Fixture()
        {
            Ranges = (SeparatePageFaultMappingRange*)NativeMemory.AlignedAlloc((nuint)(8 * sizeof(SeparatePageFaultMappingRange)), 8);
            _pageAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)(55 * sizeof(X64PageTableImagePage) + 16), 8);
            _entryAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)(Image.MaximumEntries * sizeof(ulong) + 16), 8);
            Pages = (X64PageTableImagePage*)(_pageAllocation + 8);
            Entries = (ulong*)(_entryAllocation + 8);
            PageBytes.Clear();
            EntryBytes.Clear();
            new Span<byte>(Ranges, 8 * sizeof(SeparatePageFaultMappingRange)).Clear();
            Ranges[0] = new(0x40_0000, 2, Rx);
            Ranges[1] = new(0x80_0000, 8, Rw);
            *(ulong*)_pageAllocation = Canary;
            *(ulong*)(_pageAllocation + 8 + PageBytes.Length) = Canary;
            *(ulong*)_entryAllocation = Canary;
            *(ulong*)(_entryAllocation + 8 + EntryBytes.Length) = Canary;
        }

        internal SeparatePageFaultPageTableResult Build() => Image.Build(Request, Ranges, RangeCount, 8, Pages, 55, Entries, Image.MaximumEntries);
        internal SeparatePageFaultPageTableResult Audit() => Image.Audit(Request, Ranges, RangeCount, 8, Pages, 55, Entries, Image.MaximumEntries);
        internal void AssertCanaries() => Require(*(ulong*)_pageAllocation == Canary &&
            *(ulong*)(_pageAllocation + 8 + PageBytes.Length) == Canary && *(ulong*)_entryAllocation == Canary &&
            *(ulong*)(_entryAllocation + 8 + EntryBytes.Length) == Canary, "caller span canaries");
        public void Dispose()
        {
            NativeMemory.AlignedFree(Ranges);
            NativeMemory.AlignedFree(_pageAllocation);
            NativeMemory.AlignedFree(_entryAllocation);
        }
    }
}
