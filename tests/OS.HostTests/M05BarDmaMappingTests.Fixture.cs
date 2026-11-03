using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe partial class M05BarDmaMappingTests
{
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

        public void Dispose()
        {
            NativeMemory.Free(Ranges);
            NativeMemory.Free(Pages);
            NativeMemory.Free(Entries);
        }
    }
}
