using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe class M05BarDmaMappingTests
{
    private const ulong Root = 0x100000, Writable = 0xe0030000, PageCount = 55, Nx = 1UL << 63;
    private const PageProtection Rw = PageProtection.Read | PageProtection.Write;

    [HostTest("m05.bar_dma writable ecam page preserves adjacent readonly uncached boundaries")]
    private static void BoundariesAndHardwareBits()
    {
        using var image = new Fixture();
        HostCheck.True(image.Build().Succeeded);
        foreach (ulong address in new ulong[] { 0xe0000000, 0xe002f000, Writable, 0xe0031000, 0xe00ff000 })
        {
            ulong index = image.Leaf(address);
            // Independent Present/PWT/PCD/NX oracle; only the selected page has RW.
            HostCheck.Equal(address | Nx | (address == Writable ? 0x1bUL : 0x19UL), image.Entries[index]);
            image.Entries[index] |= address == Writable ? 0x60UL : 0x20UL;
        }
        for (ulong page = 0; page < PageCount; page++)
        {
            ulong address = Root + page * 4096;
            HostCheck.Equal(address | Nx | 3UL, image.Entries[image.Leaf(address)]);
        }
        image.Entries[image.Leaf(Root)] |= 0x60; // Writable WB table backing permits Accessed/Dirty.
        image.Entries[0] |= 0x20; // A table pointer permits Accessed, but not Dirty.
        HostCheck.True(image.Audit().Succeeded);
        foreach ((ulong address, ulong xor) in new[]
        {
            (0xe002f000UL, 2UL), (0xe0031000UL, 2UL), (0xe0031000UL, 0x40UL),
            (Writable, Nx), (Writable, 8UL)
        })
        {
            ulong index = image.Leaf(address), original = image.Entries[index];
            image.Entries[index] ^= xor;
            HostCheck.True(!image.Audit().Succeeded);
            image.Entries[index] = original;
        }
        image.Entries[0] |= 0x40;
        HostCheck.True(!image.Audit().Succeeded);
        image.Entries[0] &= ~0x40UL;
        HostCheck.True(image.Audit().Succeeded);
    }

    [HostTest("m05.bar_dma broad writable uncached overlap and ordinary builds reject atomically")]
    private static void Rejections()
    {
        using var image = new Fixture();
        image.Ranges[1] = new(Writable, 2, Rw, PageCachePolicy.Uncached);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, image.Build().Status);
        image.AssertEmpty();
        image.Ranges[1] = new(Writable - 4096, 1, Rw, PageCachePolicy.Uncached);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.RangeOverlap, image.Build().Status);
        image.AssertEmpty();
        // Isolate the ordinary builder's cache restriction with an otherwise legal RW range.
        image.Ranges[0] = new(Writable, 1, Rw, PageCachePolicy.Uncached);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange,
            Image.Build(image.Request, image.Ranges, 1, 3, image.Pages, PageCount, image.Entries, PageCount * 512).Status);
        image.AssertEmpty();
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly SeparatePageFaultPageTableRequest Request;
        internal readonly SeparatePageFaultMappingRange* Ranges = (SeparatePageFaultMappingRange*)NativeMemory.AllocZeroed(3, (nuint)sizeof(SeparatePageFaultMappingRange));
        internal readonly X64PageTableImagePage* Pages = (X64PageTableImagePage*)NativeMemory.AllocZeroed((nuint)PageCount, (nuint)sizeof(X64PageTableImagePage));
        internal readonly ulong* Entries = (ulong*)NativeMemory.AllocZeroed((nuint)(PageCount * 512), sizeof(ulong));
        internal Fixture()
        {
            HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406UL, 0, 3, 255, out var pat));
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary(Root, out var root);
            Request = new(root, PageCount, X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
            Ranges[0] = new(0xe0000000, 48, PageProtection.Read, PageCachePolicy.Uncached);
            Ranges[1] = new(Writable, 1, Rw, PageCachePolicy.Uncached);
            Ranges[2] = new(0xe0031000, 207, PageProtection.Read, PageCachePolicy.Uncached);
        }
        internal SeparatePageFaultPageTableResult Build() => Image.BuildRetainedImage(Request, Ranges, 3, 3, Pages, PageCount, Entries, PageCount * 512);
        internal SeparatePageFaultPageTableResult Audit() => Image.AuditRetainedAccessedImage(Request, Ranges, 3, 3, Pages, PageCount, Entries, PageCount * 512);
        internal ulong Leaf(ulong address)
        {
            ulong table = 0;
            for (int level = 4; level > 1; level--)
            {
                ulong raw = Entries[table * 512 + ((address >> (12 + 9 * (level - 1))) & 511)];
                HostCheck.True((raw & 3) == 3 && (raw & 0x98) == 0);
                table = ((raw & 0x000ffffffffff000UL) - Root) / 4096;
                HostCheck.True(table < PageCount);
            }
            return table * 512 + ((address >> 12) & 511);
        }
        internal void AssertEmpty()
        {
            HostCheck.True(new ReadOnlySpan<byte>(Entries, (int)(PageCount * 4096)).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.True(new ReadOnlySpan<byte>(Pages, (int)PageCount * sizeof(X64PageTableImagePage)).IndexOfAnyExcept((byte)0) < 0);
        }
        public void Dispose() { NativeMemory.Free(Ranges); NativeMemory.Free(Pages); NativeMemory.Free(Entries); }
    }
}
