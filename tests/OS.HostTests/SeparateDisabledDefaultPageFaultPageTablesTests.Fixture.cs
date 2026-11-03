using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables;
using Status = OS.Kernel.Diagnostics.SeparatePageFaultPageTableStatus;

internal static unsafe partial class SeparateDisabledDefaultPageFaultPageTablesTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* _pageAllocation;
        private readonly byte* _entryAllocation;
        private const ulong Canary = 0xfeed_1234_dead_5678;
        private readonly bool _retained;
        internal readonly SeparatePageFaultMappingRange* Ranges;
        internal readonly X64PageTableImagePage* Pages;
        internal readonly ulong* Entries;
        internal ulong RangeCount = 2;
        internal SeparatePageFaultPageTableRequest Request = SeparateDisabledDefaultPageFaultPageTablesTests.Request();
        internal Span<byte> PageBytes => new(Pages, checked(55 * sizeof(X64PageTableImagePage)));
        internal Span<byte> EntryBytes => new(Entries, checked((int)Image.MaximumEntries * sizeof(ulong)));

        internal Fixture(bool retained = false)
        {
            _retained = retained;
            int rangeCapacity = (int)(retained ? Image.MaximumRetainedRanges : Image.MaximumRanges);
            Ranges = (SeparatePageFaultMappingRange*)NativeMemory.AlignedAlloc((nuint)(rangeCapacity * sizeof(SeparatePageFaultMappingRange)), 8);
            _pageAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)(55 * sizeof(X64PageTableImagePage) + 16), 8);
            _entryAllocation = (byte*)NativeMemory.AlignedAlloc((nuint)(Image.MaximumEntries * sizeof(ulong) + 16), 8);
            Pages = (X64PageTableImagePage*)(_pageAllocation + 8);
            Entries = (ulong*)(_entryAllocation + 8);
            PageBytes.Clear();
            EntryBytes.Clear();
            new Span<byte>(Ranges, rangeCapacity * sizeof(SeparatePageFaultMappingRange)).Clear();
            Ranges[0] = new(0x40_0000, 2, Rx);
            Ranges[1] = new(0x80_0000, 8, Rw);
            *(ulong*)_pageAllocation = Canary;
            *(ulong*)(_pageAllocation + 8 + PageBytes.Length) = Canary;
            *(ulong*)_entryAllocation = Canary;
            *(ulong*)(_entryAllocation + 8 + EntryBytes.Length) = Canary;
        }

        internal SeparatePageFaultPageTableResult Build() => _retained ? Image.BuildRetainedImage(Request, Ranges, RangeCount, Image.MaximumRetainedRanges, Pages, 55, Entries, Image.MaximumEntries) : Image.Build(Request, Ranges, RangeCount, 8, Pages, 55, Entries, Image.MaximumEntries);
        internal SeparatePageFaultPageTableResult Audit() => _retained ? Image.AuditRetainedImage(Request, Ranges, RangeCount, Image.MaximumRetainedRanges, Pages, 55, Entries, Image.MaximumEntries) : Image.Audit(Request, Ranges, RangeCount, 8, Pages, 55, Entries, Image.MaximumEntries);
        internal SeparatePageFaultPageTableResult AuditAccessed() => Image.AuditRetainedAccessedImage(Request, Ranges, RangeCount, Image.MaximumRetainedRanges, Pages, 55, Entries, Image.MaximumEntries);
        internal void AssertCanaries() => Require(*(ulong*)_pageAllocation == Canary && *(ulong*)(_pageAllocation + 8 + PageBytes.Length) == Canary && *(ulong*)_entryAllocation == Canary && *(ulong*)(_entryAllocation + 8 + EntryBytes.Length) == Canary, "caller span canaries");
        public void Dispose()
        {
            NativeMemory.AlignedFree(Ranges);
            NativeMemory.AlignedFree(_pageAllocation);
            NativeMemory.AlignedFree(_entryAllocation);
        }
    }
}
