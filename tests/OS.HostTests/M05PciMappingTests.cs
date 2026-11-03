using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe partial class M05PciMappingTests
{
    private const ulong Root = 0x100000, Ecam = 0xe0000000, PageCount = 55;
    private const ulong Nx = 1UL << 63;
    [HostTest("m05.pci ecam leaves are uncached readonly nx with writeback tables")]
    private static void EcamLeaves()
    {
        using var image = new Fixture();
        image.Range[0] = new(Ecam, 256, PageProtection.Read, PageCachePolicy.Uncached);
        var result = image.Build();
        HostCheck.True(result.Succeeded);
        HostCheck.Equal(PageCount + 256, result.MappedPages);
        foreach (ulong page in new ulong[]
        {
            0,
            128,
            255
        }

        )
        {
            ulong address = Ecam + page * 4096;
            ulong leaf = image.Leaf(address);
            // Independent bits: Present=1, RW=0, PWT=8, PCD=16 and NX=bit63.
            HostCheck.Equal(address | Nx | 0x19UL, image.Entries[leaf]);
            image.Entries[leaf] |= 0x20; // Hardware Accessed is permitted on read-only ECAM.
        }

        for (ulong page = 0; page < PageCount; page++)
        {
            ulong address = Root + page * 4096;
            HostCheck.Equal(address | Nx | 3UL, image.Entries[image.Leaf(address)]);
        }

        HostCheck.True(image.Audit().Succeeded);
        ulong middle = image.Leaf(Ecam + 128 * 4096), original = image.Entries[middle];
        foreach (ulong changed in new[]
        {
            original | 0x40,
            original | 2,
            original ^ 0x18,
            original ^ Nx
        }

        )
        {
            image.Entries[middle] = changed;
            HostCheck.Equal(SeparatePageFaultPageTableStatus.MappingMismatch, image.Audit().Status);
            image.Entries[middle] = original;
        }

        HostCheck.True(image.Audit().Succeeded);
        image.Entries[image.Leaf(Root)] |= 0x18;
        HostCheck.Equal(SeparatePageFaultPageTableStatus.MappingMismatch, image.Audit().Status);
    }

    [HostTest("m05.pci retained ecam bounds and invalid policies reject atomically")]
    private static void Admission()
    {
        using (var maximum = new Fixture())
        {
            maximum.Range[0] = new(Ecam, 1024, PageProtection.Read, PageCachePolicy.Uncached);
            var result = maximum.Build();
            HostCheck.True(result.Succeeded);
            HostCheck.Equal(PageCount + 1024, result.MappedPages);
            HostCheck.Equal((Ecam + 1023 * 4096) | Nx | 0x19UL, maximum.Entries[maximum.Leaf(Ecam + 1023 * 4096)]);
            HostCheck.True(maximum.Audit().Succeeded);
        }

        using var image = new Fixture();
        foreach (var range in new SeparatePageFaultMappingRange[]
        {
            new(Ecam, 1025, PageProtection.Read, PageCachePolicy.Uncached),
            new(Ecam, 256, PageProtection.Read | PageProtection.Execute, PageCachePolicy.Uncached),
            new(Ecam, 2, PageProtection.Read | PageProtection.Write, PageCachePolicy.Uncached)
        }

        )
        {
            image.Range[0] = range;
            HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, image.Build().Status);
            image.AssertEmpty();
        }

        image.Range[0] = new(Root, 256, PageProtection.Read, PageCachePolicy.Uncached);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.TableArenaOverlap, image.Build().Status);
        image.AssertEmpty();
        // An otherwise-admitted one-page RW mapping isolates ordinary-builder UC rejection.
        image.Range[0] = new(Ecam, 1, PageProtection.Read | PageProtection.Write, PageCachePolicy.Uncached);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, Image.Build(image.Request, image.Range, 1, 1, image.Pages, PageCount, image.Entries, PageCount * 512).Status);
        image.AssertEmpty();
        image.Range[0] = new(Ecam, 256, PageProtection.Read, PageCachePolicy.Uncached);
        image.Request = Request(255);
        HostCheck.Equal(SeparatePageFaultPageTableStatus.InvalidRange, image.Build().Status);
        image.AssertEmpty();
    }

    private static SeparatePageFaultPageTableRequest Request(byte uncached = 3)
    {
        HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406UL, 0, uncached, 255, out var pat));
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(Root, out var root);
        return new(root, PageCount, X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat));
    }
}
