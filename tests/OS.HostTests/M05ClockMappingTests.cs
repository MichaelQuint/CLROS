using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe class M05ClockMappingTests
{
    private const ulong Root = 0x100000, Device = 0xfed00000, PageCount = 55;
    private const PageProtection Rw = PageProtection.Read | PageProtection.Write;

    [HostTest("m05.clock uncached device leaf retains writeback tables")]
    private static void DeviceLeaf()
    {
        using var image = new Fixture();
        image.Range[0] = new(Device, 1, Rw, PageCachePolicy.Uncached);
        HostCheck.True(image.Build().Succeeded);
        ulong deviceIndex = image.Leaf(Device), arenaIndex = image.Leaf(Root);
        // Independent raw-bit oracle: selected PAT slot 3 is PCD|PWT, NX and RW.
        HostCheck.Equal(Device | (1UL << 63) | 0x1bUL, image.Entries[deviceIndex]);
        HostCheck.Equal(Root | (1UL << 63) | 3UL, image.Entries[arenaIndex]);
        image.Entries[deviceIndex] |= 0x60;
        HostCheck.True(image.Audit().Succeeded);
        image.Entries[deviceIndex] &= ~0x18UL;
        HostCheck.Equal(SeparatePageFaultPageTableStatus.MappingMismatch, image.Audit().Status);
        image.Entries[deviceIndex] |= 0x18;
        image.Entries[arenaIndex] |= 0x18;
        HostCheck.Equal(SeparatePageFaultPageTableStatus.MappingMismatch, image.Audit().Status);
    }

    [HostTest("m05.clock device range admission is atomic and bounded")]
    private static void Rejections()
    {
        using var image = new Fixture();
        foreach (var range in new SeparatePageFaultMappingRange[]
        {
            new(Device, 2, Rw, PageCachePolicy.Uncached),
            new(Device, 1025, PageProtection.Read, PageCachePolicy.Uncached),
            new(Device, 1, PageProtection.Read | PageProtection.Execute, PageCachePolicy.Uncached),
            new(Device, 1, Rw, PageCachePolicy.WriteCombining),
            new(Device, 1, Rw, PageCachePolicy.Uninitialized)
        })
        {
            image.Range[0] = range;
            HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, image.Build().Status);
            image.AssertEmpty();
        }
        image.Range[0] = new(Device, 1, Rw, PageCachePolicy.Uncached);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange,
            Image.Build(image.Request, image.Range, 1, 1, image.Pages, PageCount, image.Entries, PageCount * 512).Status);
        image.AssertEmpty();
        image.Request = Request(255);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, image.Build().Status);
        image.AssertEmpty();
    }

    private static SeparatePageFaultPageTableRequest Request(byte uncached = 3)
    {
        HostCheck.Equal(X64PageTableEntryStatus.Success,
            X64PatMappingToken.TryCreate(0x0007040600070406UL, 0, uncached, 255, out var pat));
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(Root, out var root);
        return new(root, PageCount, X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
    }

    private sealed class Fixture : IDisposable
    {
        internal SeparatePageFaultPageTableRequest Request = M05ClockMappingTests.Request();
        internal readonly SeparatePageFaultMappingRange* Range = (SeparatePageFaultMappingRange*)NativeMemory.AllocZeroed((nuint)sizeof(SeparatePageFaultMappingRange));
        internal readonly X64PageTableImagePage* Pages = (X64PageTableImagePage*)NativeMemory.AllocZeroed((nuint)PageCount, (nuint)sizeof(X64PageTableImagePage));
        internal readonly ulong* Entries = (ulong*)NativeMemory.AllocZeroed((nuint)(PageCount * 512), sizeof(ulong));
        internal SeparatePageFaultPageTableResult Build() => Image.BuildRetainedImage(Request, Range, 1, 1, Pages, PageCount, Entries, PageCount * 512);
        internal SeparatePageFaultPageTableResult Audit() => Image.AuditRetainedAccessedImage(Request, Range, 1, 1, Pages, PageCount, Entries, PageCount * 512);
        internal ulong Leaf(ulong address)
        {
            ulong table = 0;
            for (int level = 4; level > 1; level--)
            {
                ulong value = Entries[table * 512 + ((address >> (12 + 9 * (level - 1))) & 511)];
                HostCheck.True((value & 3) == 3 && (value & 0x98) == 0);
                table = ((value & 0x000fffffffffF000UL) - Root) / 4096;
                HostCheck.True(table < PageCount);
            }
            return table * 512 + ((address >> 12) & 511);
        }
        internal void AssertEmpty()
        {
            HostCheck.True(new ReadOnlySpan<byte>(Entries, (int)(PageCount * 4096)).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.True(new ReadOnlySpan<byte>(Pages, (int)PageCount * sizeof(X64PageTableImagePage)).IndexOfAnyExcept((byte)0) < 0);
        }
        public void Dispose() { NativeMemory.Free(Range); NativeMemory.Free(Pages); NativeMemory.Free(Entries); }
    }
}
