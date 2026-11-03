using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;

internal static unsafe partial class M05ClockMappingTests
{
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

        public void Dispose()
        {
            NativeMemory.Free(Range);
            NativeMemory.Free(Pages);
            NativeMemory.Free(Entries);
        }
    }
}
