using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe class M05VirtioNegotiationMappingTests
{
    private const ulong Root = 0x100000, Bar = 0xc000000000, Pages = 55, Nx = 1UL << 63;
    private const PageProtection Rw = PageProtection.Read | PageProtection.Write;

    [HostTest("m05.virtio.mapping four high BAR pages are UC NX with bounded ECAM write authority")]
    private static void InstalledImage()
    {
        var ranges = (SeparatePageFaultMappingRange*)NativeMemory.AllocZeroed(7, (nuint)sizeof(SeparatePageFaultMappingRange));
        var pages = (X64PageTableImagePage*)NativeMemory.AllocZeroed((nuint)Pages, (nuint)sizeof(X64PageTableImagePage));
        var entries = (ulong*)NativeMemory.AllocZeroed((nuint)(Pages * 512), sizeof(ulong));
        try
        {
            HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406UL, 0, 3, 255, out var pat));
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary(Root, out var root);
            var request = new SeparatePageFaultPageTableRequest(root, Pages, X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
            ranges[0] = new(0xe0000000, 48, PageProtection.Read, PageCachePolicy.Uncached);
            ranges[1] = new(0xe0030000, 1, Rw, PageCachePolicy.Uncached);
            ranges[2] = new(0xe0031000, 207, PageProtection.Read, PageCachePolicy.Uncached);
            for (ulong i = 0; i < 4; i++) ranges[3 + i] = new(Bar + i * 4096, 1, Rw, PageCachePolicy.Uncached);
            HostCheck.True(Image.BuildRetainedImage(request, ranges, 7, 7, pages, Pages, entries, Pages * 512).Succeeded);
            ulong Leaf(ulong address)
            {
                ulong table = 0;
                for (int level = 4; level > 1; level--)
                {
                    ulong raw = entries[table * 512 + ((address >> (12 + 9 * (level - 1))) & 511)];
                    HostCheck.True((raw & 3) == 3 && (raw & 0x98) == 0);
                    table = ((raw & 0x000ffffffffff000UL) - Root) / 4096;
                    HostCheck.True(table < Pages);
                }
                return table * 512 + ((address >> 12) & 511);
            }
            bool Audit() => Image.AuditRetainedAccessedImage(request, ranges, 7, 7, pages, Pages, entries, Pages * 512).Succeeded;
            for (ulong i = 0; i < 4; i++)
            {
                ulong address = Bar + i * 4096, index = Leaf(address);
                HostCheck.Equal(address | Nx | 0x1bUL, entries[index]);
                entries[index] |= 0x60;
            }
            HostCheck.Equal(0UL, entries[Leaf(Bar + 0x4000)]);
            HostCheck.Equal(0xe002f000UL | Nx | 0x19UL, entries[Leaf(0xe002f000)]);
            HostCheck.Equal(0xe0030000UL | Nx | 0x1bUL, entries[Leaf(0xe0030000)]);
            HostCheck.Equal(0xe0031000UL | Nx | 0x19UL, entries[Leaf(0xe0031000)]);
            HostCheck.True(Audit());
            foreach (ulong change in new ulong[] { 2, 8, 16, Nx, 0x1000 })
            {
                ulong index = Leaf(Bar + 0x2000), saved = entries[index];
                entries[index] ^= change; HostCheck.True(!Audit()); entries[index] = saved;
            }
            entries[Leaf(Bar + 0x4000)] = Bar + 0x4000 | Nx | 0x1b;
            HostCheck.True(!Audit());
        }
        finally { NativeMemory.Free(ranges); NativeMemory.Free(pages); NativeMemory.Free(entries); }
    }
}
