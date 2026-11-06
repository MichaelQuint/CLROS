using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Platform;

internal static unsafe class PostFirmwarePhysicalMappingExplicitUCWCWBTests
{
    private const ulong Nx = 1UL << 63;
    private const ulong Base = 0x200000;
    private const ulong Table = 0x800000;
    private const ulong Sentinel = 0xBADCAFE;

    [HostFixture]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m05.001.split-preparation");
        Run("policy and neighboring translations", PolicyAndNeighbors);
        Run("large PAT conversion and permissions", PatAndPermissions);
        Run("range and input rejection is atomic", RejectionIsAtomic);
        Run("last page and full span", RangeBoundaries);
        Run("identity audit prepares only unused table", IdentityImagePreparation);
        Run("identity audit rejects aliases and bad ownership", IdentityImageRejection);
        Run("MTRR WB coverage and unsupported environment", MtrrCoverage);
        Run("native emission bounds and readback", NativeEmission);
        Run("separate canonical-zero guard publication bytes and rejection", GuardNativeEmission);
        Console.WriteLine("TEST_PASS m05.001.split-preparation");
    }

    private static void Run(string name, Action test) => HostCheck.Run("m05.001.split-preparation " + name, test);

    private static void PolicyAndNeighbors()
    {
        ulong* entries = stackalloc ulong[514];
        entries[0] = entries[513] = Sentinel;
        foreach ((PhysicalMappingUsage usage, ulong cacheBits) in new[]
        {
            (PhysicalMappingUsage.Normal, 0UL),
            (PhysicalMappingUsage.Device, 0x18UL),
            (PhysicalMappingUsage.Framebuffer, 0x80UL)
        })
        {
            // Independently constructed WB, RW/NX large leaf with A/D set.
            var result = Prepare(Base | Nx | 0xE3, Base + 3 * 4096, 2 * 4096,
                usage, entries + 1);
            Require(result.IsPrepared && result.ReplacementPde == (Table | 0x23), "parent");
            for (int i = 0; i < 512; i++)
            {
                ulong expected = Base + (ulong)i * 4096;
                expected |= Nx | 0x63;
                if (i is 3 or 4)
                    expected |= cacheBits;
                Require(entries[i + 1] == expected, "child " + i);
            }
            Require(entries[0] == Sentinel && entries[513] == Sentinel, "buffer bounds");
        }
        Require(!default(PhysicalMappingPreparationResult).IsPrepared, "default result");
        Require(PostFirmwarePhysicalMappingExplicitUCWCWB.GetCachePolicy(default)
            == PageCachePolicy.Uninitialized, "default usage");
    }

    private static void PatAndPermissions()
    {
        ulong* entries = stackalloc ulong[512];
        // Large-page WC uses bit 12. Children use bit 7. Keep RX permissions.
        var result = Prepare(Base | 0x1081, Base + 4096, 4096,
            PhysicalMappingUsage.Normal, entries);
        Require(result.IsPrepared && result.ReplacementPde == (Table | 3), "WC parent");
        for (int i = 0; i < 512; i++)
        {
            ulong expected = Base + (ulong)i * 4096;
            expected |= i == 1 ? 1UL : 0x81UL;
            Require(entries[i] == expected, "WC child " + i);
        }
        result = Prepare(Base | Nx | 0x81, Base, 4096,
            PhysicalMappingUsage.Device, entries);
        Require(result.IsPrepared && entries[0] == (Base | Nx | 0x19), "read-only NX");
    }

    private static void RejectionIsAtomic()
    {
        ulong* entries = stackalloc ulong[512];
        for (int i = 0; i < 512; i++)
            entries[i] = Sentinel;
        ulong pde = Base | Nx | 0x83;
        foreach ((ulong start, ulong length) in new[]
        {
            (0UL, 4096UL), (Base, 0UL), (Base + 1, 4096UL),
            (Base, 4095UL), (ulong.MaxValue - 4095, 4096UL)
        })
        {
            Rejected(Prepare(pde, start, length, PhysicalMappingUsage.Device, entries),
                PhysicalMappingPreparationStatus.InvalidRange, entries);
        }
        foreach ((ulong start, ulong length) in new[]
        {
            (Base - 4096, 4096UL), (Base + 0x200000, 4096UL),
            (Base + 0x1FF000, 8192UL)
        })
        {
            Rejected(Prepare(pde, start, length, PhysicalMappingUsage.Device, entries),
                PhysicalMappingPreparationStatus.RangeOutsideLargePage, entries);
        }
        foreach (ulong invalidPde in new[] { 0UL, Base | 3, Base | 0x83, pde | 0x100 })
        {
            Rejected(Prepare(invalidPde, Base, 4096, PhysicalMappingUsage.Device, entries),
                PhysicalMappingPreparationStatus.InvalidInheritedEntry, entries);
        }
        Rejected(Prepare(pde, Base, 4096, default, entries),
            PhysicalMappingPreparationStatus.InvalidUsage, entries);
        Rejected(Prepare(pde, Base, 4096, (PhysicalMappingUsage)255, entries),
            PhysicalMappingPreparationStatus.InvalidUsage, entries);
        foreach (ulong invalidTable in new[] { 0UL, Table + 1, 1UL << 48 })
        {
            Rejected(Prepare(pde, Base, 4096, PhysicalMappingUsage.Device, entries,
                table: invalidTable), PhysicalMappingPreparationStatus.InvalidTableAddress, entries);
        }
        Rejected(Prepare(pde, Base, 4096, PhysicalMappingUsage.Device, entries, table: Base),
            PhysicalMappingPreparationStatus.TableOverlapsMappedRange, entries);
        Rejected(Prepare(pde, Base, 4096, PhysicalMappingUsage.Device, entries, capacity: 511),
            PhysicalMappingPreparationStatus.InvalidDestination, entries);
        foreach (ulong* invalidDestination in new ulong*[]
            { null, (ulong*)((byte*)entries + 1), (ulong*)(ulong.MaxValue - 7) })
        {
            Rejected(Prepare(pde, Base, 4096, PhysicalMappingUsage.Device, invalidDestination),
                PhysicalMappingPreparationStatus.InvalidDestination, entries);
        }
        var unsupported = Prepare(pde, Base, 4096, PhysicalMappingUsage.Framebuffer,
            entries, supportsWc: false);
        Rejected(unsupported, PhysicalMappingPreparationStatus.UnsupportedCachePolicy, entries);
        Require(unsupported.EntryStatus == X64PageTableEntryStatus.CachePolicyNotSupported,
            "unsupported WC must not fall back");
        var uninitialized = PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareTwoMiBSplit(
            pde, Address(Base), 4096, PhysicalMappingUsage.Device,
            Address(Table), entries, 512, default);
        Rejected(uninitialized, PhysicalMappingPreparationStatus.InvalidInheritedEntry, entries);
    }

    private static void RangeBoundaries()
    {
        ulong* entries = stackalloc ulong[512];
        var last = Prepare(Base | Nx | 0x83, Base + 0x1FF000, 4096,
            PhysicalMappingUsage.Device, entries);
        Require(last.IsPrepared && entries[510] == (Base + 510 * 4096 | Nx | 3)
            && entries[511] == (Base + 511 * 4096 | Nx | 0x1B), "last page");
        var full = Prepare(Base | Nx | 0x83, Base, 0x200000,
            PhysicalMappingUsage.Framebuffer, entries);
        Require(full.IsPrepared, "full span");
        for (int i = 0; i < 512; i++)
            Require(entries[i] == (Base + (ulong)i * 4096 | Nx | 0x83), "full child " + i);
    }

    private static PhysicalMappingPreparationResult Prepare(
        ulong pde, ulong start, ulong length, PhysicalMappingUsage usage,
        ulong* destination, uint capacity = 512, ulong table = Table, bool supportsWc = true)
    {
        return PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareTwoMiBSplit(
            pde, Address(start), length, usage, Address(table), destination, capacity, Conditions(supportsWc));
    }

    private static X64PageTableEncodingPreconditions Conditions(bool supportsWc = true)
    {
        Require(X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3,
            supportsWc ? (byte)4 : X64PatMappingToken.UnsupportedIndex, out var pat)
            == X64PageTableEntryStatus.Success, "PAT fixture");
        return X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat);
    }

    private static ulong[] IdentityImage()
    {
        // Four used tables plus one mapped but unused page for the split.
        ulong[] entries = new ulong[5 * 512];
        entries[0] = Table + 4096 | 3;
        entries[512] = Table + 8192 | 3;
        entries[1025] = Base | Nx | 0xE3;
        entries[1028] = Table + 12288 | 3;
        for (int i = 0; i < 5; i++)
            entries[1536 + i] = Table + (ulong)i * 4096 | Nx | 3;
        return entries;
    }

    private static void IdentityImagePreparation()
    {
        ulong[] entries = IdentityImage();
        ulong[] before = (ulong[])entries.Clone();
        fixed (ulong* image = entries)
        {
            var result = PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(
                Address(Table), image, 5, Address(Base + 4096), 4096,
                PhysicalMappingUsage.Device, Conditions());
            Require(result.IsPrepared && result.ParentEntryIndex == 1025
                && result.ChildTablePageIndex == 4 && result.OriginalPde == before[1025]
                && result.ReplacementPde == (Table + 16384 | 0x23), "prepared ownership indices");
            for (int i = 0; i < 2048; i++)
                Require(entries[i] == before[i], "reachable entry changed");
            for (int i = 0; i < 512; i++)
                Require(entries[2048 + i] == (Base + (ulong)i * 4096 | Nx | (i == 1 ? 0x7BUL : 0x63UL)),
                    "split leaf " + i);

            // Simulate publication in host storage. A repeat request cannot
            // reuse the table now linked into the image or split an existing PTE.
            image[result.ParentEntryIndex] = result.ReplacementPde;
            ulong[] published = (ulong[])entries.Clone();
            var repeat = PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(
                Address(Table), image, 5, Address(Base + 4096), 4096,
                PhysicalMappingUsage.Framebuffer, Conditions());
            Require(repeat.Status == PhysicalMappingPreparationStatus.TargetNotLargePage
                && entries.SequenceEqual(published), "repeat altered image");
        }
    }

    private static void IdentityImageRejection()
    {
        foreach ((int index, ulong replacement, PhysicalMappingPreparationStatus expected) in new[]
        {
            (1026, Base | Nx | 0x83, PhysicalMappingPreparationStatus.NonIdentityMapping),
            (513, Table + 8192 | 3, PhysicalMappingPreparationStatus.SharedOrCyclicTable),
            (0, Table | 3, PhysicalMappingPreparationStatus.SharedOrCyclicTable),
            (512, Table + 5 * 4096 | 3, PhysicalMappingPreparationStatus.InvalidTableArena),
            (1536, Table | Nx | 0x1B, PhysicalMappingPreparationStatus.UnmappedTableStorage),
            (1540, 0UL, PhysicalMappingPreparationStatus.UnmappedTableStorage),
            (2048, 1UL, PhysicalMappingPreparationStatus.NonzeroUnusedTable),
            (1025, Base | 0x83, PhysicalMappingPreparationStatus.MalformedTable)
        })
        {
            ulong[] entries = IdentityImage();
            entries[index] = replacement;
            ulong[] before = (ulong[])entries.Clone();
            fixed (ulong* image = entries)
            {
                var result = PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(
                    Address(Table), image, 5, Address(Base), 4096, PhysicalMappingUsage.Device, Conditions());
                Require(result.Status == expected && entries.SequenceEqual(before),
                    "image rejection expected=" + expected + " actual=" + result.Status);
            }
        }
        ulong[] exhausted = IdentityImage();
        fixed (ulong* image = exhausted)
        {
            foreach (uint count in new uint[] { 0, 65 })
                Require(PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(Address(Table), image,
                    count, Address(Base), 4096, PhysicalMappingUsage.Normal, Conditions()).Status
                    == PhysicalMappingPreparationStatus.InvalidTableArena, "arena bound");
            ulong[] before = (ulong[])exhausted.Clone();
            Require(PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(Address(Table), image,
                4, Address(Base), 4096, PhysicalMappingUsage.Device, Conditions()).Status
                == PhysicalMappingPreparationStatus.NoFreeTablePage && exhausted.SequenceEqual(before), "exhaustion");
        }

        // Root/PDPT occupy the end of one large leaf; the free child table is
        // across the 2 MiB boundary. Checking only the new child misses this.
        ulong[] straddling = new ulong[5 * 512];
        straddling[0] = 0x7FF003;
        straddling[512] = 0x800003;
        straddling[1027] = 0x600083 | Nx;
        straddling[1028] = 0x801003;
        for (int i = 0; i < 3; i++)
            straddling[1536 + i] = 0x800000 + (ulong)i * 4096 | Nx | 3;
        ulong[] original = (ulong[])straddling.Clone();
        fixed (ulong* image = straddling)
        {
            Require(PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareIdentitySplit(Address(0x7FE000), image,
                5, Address(0x7FF000), 4096, PhysicalMappingUsage.Device, Conditions()).Status
                == PhysicalMappingPreparationStatus.TableOverlapsMappedRange && straddling.SequenceEqual(original),
                "split would unmap or recache existing table storage");
        }
    }

    private static PhysicalAddress Address(ulong raw)
    {
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(raw, out var address);
        return address;
    }

    private static void NativeEmission()
    {
        byte* code = stackalloc byte[1024];
        for (int i = 0; i < 1024; i++)
            code[i] = 0xA5;
        Require(!X64PhysicalMappingPublication.Emit(code + 1, X64PhysicalMappingPublication.RequiredBytes - 1)
            && !X64PhysicalMappingPublication.Emit(null, 1024), "publication capacity");
        for (int i = 0; i < 1024; i++)
            Require(code[i] == 0xA5, "failed emission wrote storage");
        Require(X64PhysicalMappingPublication.Emit(code + 1, X64PhysicalMappingPublication.RequiredBytes)
            && X64PhysicalMappingPublication.ValidateReadBack(code + 1, X64PhysicalMappingPublication.RequiredBytes),
            "publication bytes");
        Require(code[0] == 0xA5 && code[1 + X64PhysicalMappingPublication.RequiredBytes] == 0xA5,
            "publication bounds");
        code[1 + 0x93] ^= 1;
        Require(!X64PhysicalMappingPublication.ValidateReadBack(code + 1, X64PhysicalMappingPublication.RequiredBytes),
            "mutated CR3 instruction admitted");
        Require(X64PhysicalMappingPublication.WritePatBytes == 263
            && !X64PhysicalMappingPublication.EmitWritePat(code + 512, 262), "PAT capacity");
        Require(X64PhysicalMappingPublication.EmitWritePat(code + 512, 263)
            && X64PhysicalMappingPublication.ValidateWritePatReadBack(code + 512, 263), "PAT bytes");
        Require(code[511] == 0xA5 && code[775] == 0xA5, "PAT bounds");
        code[512 + 230] ^= 1;
        Require(!X64PhysicalMappingPublication.ValidateWritePatReadBack(code + 512, 263), "PAT mutation admitted");
        Require(X64PhysicalMappingPublication.EmitReadMsr(code, 14)
            && !X64PhysicalMappingPublication.EmitReadMsr(code, 13), "MSR bytes");
        Require(X64PhysicalMappingPublication.EmitReadCapabilities(code, 55)
            && !X64PhysicalMappingPublication.EmitReadCapabilities(code, 54), "CPUID bytes");
    }

    private static void GuardNativeEmission()
    {
        byte* code = stackalloc byte[1024];
        new Span<byte>(code, 1024).Fill(0xa5);
        HostCheck.Equal(174U, X64PhysicalMappingPublication.GuardRequiredBytes);
        foreach ((ulong Pointer, uint Capacity) bad in new (ulong, uint)[]
        {
            (0, 174), ((ulong)code + 1, 173), (1, 174),
            (0x0000800000000000 - 174, 174), (ulong.MaxValue - 7, 174)
        })
        {
            Require(!X64PhysicalMappingPublication.EmitGuardPublication((byte*)bad.Pointer, bad.Capacity), "guard malformed output admitted");
            Require(!X64PhysicalMappingPublication.ValidateGuardPublication((byte*)bad.Pointer, bad.Capacity), "guard malformed readback admitted");
            Require(new ReadOnlySpan<byte>(code, 1024).ToArray().All(b => b == 0xa5), "guard rejection wrote bytes");
        }
        Require(X64PhysicalMappingPublication.EmitGuardPublication(code + 1, 174) &&
            X64PhysicalMappingPublication.ValidateGuardPublication(code + 1, 174), "guard emission/readback");
        Require(code[0] == 0xa5 && code[175] == 0xa5, "guard output bounds");
        // Independent x64 vectors: exact-zero skips, nonzero present tests,
        // expected-value refusal and the complete revoke/flush/install tail.
        byte[] expectedTail = Convert.FromHexString(
            "4885D27409F6C2010F8433000000" +
            "4D85C0740A41F6C0010F8424000000" +
            "4839110F851B000000" +
            "0FAEF04989D24983E2FE4C89110F22D84C89010F09B801000000C331C0C3");
        Require(new ReadOnlySpan<byte>(code + 107, 68).SequenceEqual(expectedTail), "guard independent instruction vector");
        foreach (int branch in new[] { 9, 22, 36, 52, 66, 78, 87, 100, 114, 129, 138 })
        {
            Require(code[1 + branch] == 0x0f && (code[2 + branch] is 0x84 or 0x85), "guard rejection opcode");
            int displacement = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(code + 3 + branch, 4));
            Require(branch + 6 + displacement == 171, "guard refusal must precede all live writes");
        }
        code[111] ^= 1; // First exact-zero short skip displacement.
        Require(!X64PhysicalMappingPublication.ValidateGuardPublication(code + 1, 174), "guard short-branch mutation admitted");
        Require(X64PhysicalMappingPublication.Emit(code + 256, 164), "original publisher emission");
        byte[] originalGuards = Convert.FromHexString("F6C2010F842E00000041F6C0010F8424000000");
        Require(new ReadOnlySpan<byte>(code + 362, 19).SequenceEqual(originalGuards), "original present-only guards changed");
        Require(!X64PhysicalMappingPublication.ValidateGuardPublication(code + 256, 174), "old publisher admitted as guard variant");
        Require(X64PhysicalMappingPublication.EmitGuardPublication(code + 1, 174) &&
            !X64PhysicalMappingPublication.ValidateReadBack(code + 1, 174), "guard variant admitted as old publisher");
    }

    private static void MtrrCoverage()
    {
        ulong* bases = stackalloc ulong[2];
        ulong* masks = stackalloc ulong[2];
        const ulong addressMask = 0x0000_FFFF_FFFF_F000;
        const ulong enabled = 0x800;
        const ulong cr0 = 0x80010001;
        Require(PostFirmwarePhysicalMappingExplicitUCWCWB.ValidateWriteBackMtrrRange(Address(Base), 4096,
            48, cr0, 0, enabled | 6, false, null, null, 0) == PhysicalMappingPreparationStatus.Prepared,
            "default WB with no variable ranges");
        bases[0] = Base | 6;
        bases[1] = Base + 0x200000 | 6;
        masks[0] = masks[1] = (addressMask & ~0x1FFFFFUL) | enabled;
        Require(Mtrr(bases, masks, 0x400000) == PhysicalMappingPreparationStatus.Prepared,
            "adjacent WB union over default UC");
        bases[1] = Base + 0x400000 | 6;
        Require(Mtrr(bases, masks, 0x400000) == PhysicalMappingPreparationStatus.MtrrRangeNotWriteBack,
            "gap in WB union");
        bases[1] = Base + 0x200000;
        Require(Mtrr(bases, masks, 0x400000, enabled | 6) == PhysicalMappingPreparationStatus.MtrrRangeNotWriteBack,
            "overlapping UC overrides default WB");
        bases[1] = Base + 0x200000 | 6;
        masks[0] ^= 1UL << 23;
        Require(Mtrr(bases, masks, 0x400000) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "discontinuous mask");
        masks[0] ^= 1UL << 23;
        bases[0] |= 0x1000;
        Require(Mtrr(bases, masks, 0x400000) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "base misaligned to size");
        bases[0] = Base | 6;
        masks[0] |= 1UL << 48;
        Require(Mtrr(bases, masks, 0x400000) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "mask exceeds physical width");
        masks[0] &= ~(1UL << 48);
        foreach ((ulong control, ulong cap, bool tme) in new[]
        {
            (cr0 | 0x40000000, 2UL, false), (cr0 | 0x20000000, 2UL, false),
            (cr0, 2UL | 0x800, false), (cr0, 2UL, true)
        })
            Require(PostFirmwarePhysicalMappingExplicitUCWCWB.ValidateWriteBackMtrrRange(Address(Base), 4096,
                48, control, cap, enabled, tme, bases, masks, 2)
                == PhysicalMappingPreparationStatus.UnsupportedCacheEnvironment, "unsupported controls/SMRR/TME");
        Require(Mtrr(bases, masks, 4096, 0) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "disabled MTRRs");
        Require(PostFirmwarePhysicalMappingExplicitUCWCWB.ValidateWriteBackMtrrRange(Address(0x80000), 4096,
            48, cr0, 2, enabled, false, bases, masks, 2) == PhysicalMappingPreparationStatus.InvalidRange,
            "fixed-MTRR range not admitted");
        Require(PostFirmwarePhysicalMappingExplicitUCWCWB.ValidateWriteBackMtrrRange(Address(Base), 4096,
            48, cr0, 3, enabled, false, bases, masks, 2) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "incomplete snapshot");
        bases[1] = Base + 0x200000 | 1;
        Require(Mtrr(bases, masks, 4096, enabled | 6) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "unsupported WC outside target still invalidates snapshot");
        Require(Mtrr(bases, masks, 4096, enabled | 1) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "unsupported WC default");
        bases[1] = Base + 0x200000 | 6;
        Require(Mtrr(bases, masks, 4096, 0xC06) == PhysicalMappingPreparationStatus.InvalidMtrrSnapshot,
            "enabled fixed MTRRs require capability");
    }

    private static PhysicalMappingPreparationStatus Mtrr(ulong* bases, ulong* masks, ulong bytes,
        ulong defaultType = 0x800) => PostFirmwarePhysicalMappingExplicitUCWCWB.ValidateWriteBackMtrrRange(
            Address(Base), bytes, 48, 0x80010001, 2, defaultType, false, bases, masks, 2);

    private static void Rejected(PhysicalMappingPreparationResult result,
        PhysicalMappingPreparationStatus expected, ulong* entries)
    {
        Require(!result.IsPrepared && result.ReplacementPde == 0 && result.Status == expected,
            "rejection expected=" + expected + " actual=" + result.Status);
        for (int i = 0; i < 512; i++)
            Require(entries[i] == Sentinel, "failure wrote child " + i);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
