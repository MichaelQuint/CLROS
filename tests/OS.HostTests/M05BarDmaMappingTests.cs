using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe partial class M05BarDmaMappingTests
{
    private const ulong Root = 0x100000, Writable = 0xe0030000, PageCount = 55, Nx = 1UL << 63;
    private const PageProtection Rw = PageProtection.Read | PageProtection.Write;
    [HostTest("m05.bar_dma writable ecam page preserves adjacent readonly uncached boundaries")]
    private static void BoundariesAndHardwareBits()
    {
        using var image = new Fixture();
        HostCheck.True(image.Build().Succeeded);
        foreach (ulong address in new ulong[]
        {
            0xe0000000,
            0xe002f000,
            Writable,
            0xe0031000,
            0xe00ff000
        }

        )
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
            (0xe002f000UL, 2UL),
            (0xe0031000UL, 2UL),
            (0xe0031000UL, 0x40UL),
            (Writable, Nx),
            (Writable, 8UL)
        }

        )
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
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, Image.Build(image.Request, image.Ranges, 1, 3, image.Pages, PageCount, image.Entries, PageCount * 512).Status);
        image.AssertEmpty();
    }
}
