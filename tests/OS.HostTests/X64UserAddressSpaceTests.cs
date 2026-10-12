using System.Runtime.InteropServices;
using OS.Kernel.Memory;

internal static unsafe class X64UserAddressSpaceTests
{
    private const ulong Root = 0x1000000;
    private const uint Pages = 16;
    private static X64PageTableEncodingPreconditions Conditions()
    {
        HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406, 0, 3, 255, out var pat));
        return X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat);
    }
    private static X64UserMappingRange[] Ranges() =>
    [
        new() { VirtualStart = 0x100000, PhysicalStart = 0x100000, Pages = 1, Protection = PageProtection.Read | PageProtection.Execute, Cache = PageCachePolicy.WriteBack },
        new() { VirtualStart = 0x200000, PhysicalStart = 0x200000, Pages = 1, Protection = PageProtection.Read | PageProtection.Write, Cache = PageCachePolicy.WriteBack },
        new() { VirtualStart = 0x400000, PhysicalStart = 0x300000, Pages = 2, Protection = PageProtection.Read | PageProtection.Execute, Cache = PageCachePolicy.WriteBack, User = true },
        new() { VirtualStart = 0x6ffff000, PhysicalStart = 0x500000, Pages = 1, Protection = PageProtection.Read | PageProtection.Write, Cache = PageCachePolicy.WriteBack, User = true }
    ];
    private static ulong* Leaf(ulong* tables, ulong va)
    {
        uint page = 0;
        for (int level = 3; level > 0; level--)
            page = (uint)(((tables[page * 512 + ((va >> (12 + level * 9)) & 511)] & 0x000ffffffffff000) - Root) / 4096);
        return tables + page * 512 + ((va >> 12) & 511);
    }

    [HostTest("m16-004 independent user supervisor NX page encodings and absent guards", HostLane.Fast, 0)]
    private static void Encodings()
    {
        var ranges = Ranges(); var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            new Span<ulong>(tables, (int)Pages * 512).Clear();
            fixed (X64UserMappingRange* r = ranges)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var result = X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                HostCheck.True(result.Succeeded && result.Root == Root && result.UsedTables == 9 && result.LeafPages == 21 && allocated == 0);
                HostCheck.True(X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                HostCheck.Equal(0x100001UL, *Leaf(tables, 0x100000));
                HostCheck.Equal(0x8000000000200003UL, *Leaf(tables, 0x200000));
                HostCheck.Equal(0x300005UL, *Leaf(tables, 0x400000));
                HostCheck.Equal(0x301005UL, *Leaf(tables, 0x401000));
                HostCheck.Equal(0x8000000000500007UL, *Leaf(tables, 0x6ffff000));
                HostCheck.Equal(0x8000000001000003UL, *Leaf(tables, Root));
                foreach (ulong va in new ulong[] { 0, 0x402000, 0x6fffe000, 0x80000000 })
                    HostCheck.True(X64UserAddressSpace.TryReadLeaf(tables, Root, Pages, va, out ulong leaf) && leaf == 0);
                HostCheck.True(!X64UserAddressSpace.TryReadLeaf(tables, Root, Pages, 0x100000000, out _));
            }
        }
        finally { NativeMemory.AlignedFree(tables); }
    }

    [HostTest("m16-004 malformed overlap W-X table exhaustion dirty arena reject before writes", HostLane.Fast, 1)]
    private static void Rejections()
    {
        var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            for (int variant = 0; variant < 8; variant++)
            {
                var ranges = Ranges(); new Span<ulong>(tables, (int)Pages * 512).Clear();
                X64UserAddressSpaceStatus expected = X64UserAddressSpaceStatus.InvalidRange;
                if (variant == 0) ranges[2].Pages = 0;
                if (variant == 1) ranges[2].VirtualStart++;
                if (variant == 2) ranges[2].PhysicalStart = 0xfffff000;
                if (variant == 3) { ranges[2].PhysicalStart = ranges[0].PhysicalStart; expected = X64UserAddressSpaceStatus.OverlappingRanges; }
                if (variant == 4) { ranges[2].VirtualStart = Root; expected = X64UserAddressSpaceStatus.OverlappingRanges; }
                if (variant == 5) { ranges[2].Protection |= PageProtection.Write; expected = X64UserAddressSpaceStatus.InvalidEncoding; }
                if (variant == 6) expected = X64UserAddressSpaceStatus.InsufficientTables;
                if (variant == 7) { tables[0] = 0xa5; expected = X64UserAddressSpaceStatus.DirtyTables; }
                fixed (X64UserMappingRange* r = ranges)
                {
                    var result = X64UserAddressSpace.Build(r, 4, tables, Root, variant == 6 ? 4U : Pages, conditions);
                    HostCheck.Equal(expected, result.Status);
                    HostCheck.True(!result.Succeeded && result.Root == 0 && result.UsedTables == 0);
                    HostCheck.Equal(variant == 7 ? 0xa5UL : 0UL, tables[0]);
                    HostCheck.True(new ReadOnlySpan<ulong>(tables + 1, (int)Pages * 512 - 1).IndexOfAnyExcept(0UL) == -1);
                }
            }
            HostCheck.True(X64UserAddressSpace.Clear(tables, Pages));
            HostCheck.True(new ReadOnlySpan<ulong>(tables, (int)Pages * 512).IndexOfAnyExcept(0UL) == -1);
        }
        finally { NativeMemory.AlignedFree(tables); }
    }

    [HostTest("m16-004 whole-root audit rejects forged permissions aliases cycles extra leaves and unused tables", HostLane.Fast, 2)]
    private static void AuditMutations()
    {
        var ranges = Ranges(); var conditions = Conditions();
        ulong* tables = (ulong*)NativeMemory.AlignedAlloc(Pages * 4096, 4096);
        try
        {
            fixed (X64UserMappingRange* r = ranges)
            {
                for (int variant = 0; variant < 7; variant++)
                {
                    new Span<ulong>(tables, (int)Pages * 512).Clear();
                    var result = X64UserAddressSpace.Build(r, 4, tables, Root, Pages, conditions);
                    HostCheck.True(result.Succeeded);
                    if (variant == 0) *Leaf(tables, 0x400000) &= ~4UL;
                    if (variant == 1) *Leaf(tables, 0x100000) |= 4;
                    if (variant == 2) *Leaf(tables, 0x6ffff000) &= ~0x8000000000000000UL;
                    if (variant == 3) *Leaf(tables, 0x400000) |= 2;
                    if (variant == 4) *Leaf(tables, 0x402000) = 0x300005;
                    if (variant == 5) tables[0] = Root | 7;
                    if (variant == 6) tables[(Pages - 1) * 512] = 1;
                    HostCheck.True(!X64UserAddressSpace.Audit(r, 4, tables, Root, Pages, conditions, result));
                }
            }
        }
        finally { NativeMemory.AlignedFree(tables); }
    }
}
