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

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m05.001.split-preparation");
        Run("policy and neighboring translations", PolicyAndNeighbors);
        Run("large PAT conversion and permissions", PatAndPermissions);
        Run("range and input rejection is atomic", RejectionIsAtomic);
        Run("last page and full span", RangeBoundaries);
        Console.WriteLine("TEST_PASS m05.001.split-preparation");
    }

    private static void Run(string name, Action test)
    {
        string marker = "m05.001.split-preparation " + name;
        Console.WriteLine("TEST_START " + marker);
        try
        {
            test();
            Console.WriteLine("TEST_PASS " + marker);
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL " + marker + " " + error.Message);
            throw;
        }
    }

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
                if (i is 3 or 4) expected |= cacheBits;
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
        for (int i = 0; i < 512; i++) entries[i] = Sentinel;
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
        Require(X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3,
            supportsWc ? (byte)4 : X64PatMappingToken.UnsupportedIndex, out var pat)
            == X64PageTableEntryStatus.Success, "PAT fixture");
        return PostFirmwarePhysicalMappingExplicitUCWCWB.PrepareTwoMiBSplit(
            pde, Address(start), length, usage, Address(table), destination, capacity,
            X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
    }

    private static PhysicalAddress Address(ulong raw)
    {
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(raw, out var address);
        return address;
    }

    private static void Rejected(PhysicalMappingPreparationResult result,
        PhysicalMappingPreparationStatus expected, ulong* entries)
    {
        Require(!result.IsPrepared && result.ReplacementPde == 0 && result.Status == expected,
            "rejection expected=" + expected + " actual=" + result.Status);
        for (int i = 0; i < 512; i++) Require(entries[i] == Sentinel, "failure wrote child " + i);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
