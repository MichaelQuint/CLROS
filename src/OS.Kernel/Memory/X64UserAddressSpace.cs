using OS.Boot;

namespace OS.Kernel.Memory;

public enum X64UserAddressSpaceStatus : byte
{ Uninitialized, Ready, InvalidInput, InvalidRange, OverlappingRanges, InvalidEncoding, InsufficientTables, DirtyTables, AuditFailed }

public struct X64UserMappingRange
{
    public ulong VirtualStart, PhysicalStart, Pages;
    public PageProtection Protection;
    public PageCachePolicy Cache;
    public bool User;
}

public struct X64UserAddressSpaceResult
{
    public X64UserAddressSpaceStatus Status;
    public ulong Root, LeafPages;
    public uint UsedTables;
    public readonly bool Succeeded => Status == X64UserAddressSpaceStatus.Ready;
}

/// <summary>
/// Bounded low-4GiB, four-level, 4KiB-page root in exclusive caller-owned zero
/// tables. Existing canonical entry encoding supplies NX, W^X and observed PAT
/// rules. U/S is added explicitly to table pointers and declared user leaves;
/// kernel leaves and the entire table arena retain supervisor RW/NX access.
/// No physical aliases are admitted. Caller proves allocation/reservations,
/// current CPU preconditions, identity table access and CR3 publication.
/// </summary>
public static unsafe class X64UserAddressSpace
{
    public const uint MaximumRanges = 64, MaximumTables = 128;
    private const ulong Page = 4096, End = 0x100000000, AddressMask = 0x000ffffffffff000;

    public static X64UserAddressSpaceResult Build(X64UserMappingRange* ranges, uint count,
        ulong* tables, ulong physicalRoot, uint tablePages, X64PageTableEncodingPreconditions conditions)
    {
        var status = Admit(ranges, count, tables, physicalRoot, tablePages, conditions, out uint required, out ulong leaves);
        if (status != X64UserAddressSpaceStatus.Ready) return Failure(status);
        for (uint i = 0; i < tablePages * 512; i++) if (tables[i] != 0) return Failure(X64UserAddressSpaceStatus.DirtyTables);
        uint used = 1;
        for (uint index = 0; index <= count; index++)
        {
            var range = index == count ? Arena(physicalRoot, tablePages) : ranges[index];
            for (ulong page = 0; page < range.Pages; page++)
            {
                ulong va = range.VirtualStart + page * Page;
                uint pdpt = Child(tables, physicalRoot, tablePages, 0, 0, X64PageTableLevel.Pml4e, ref used, conditions);
                if (pdpt == 0) return Rollback(tables, tablePages);
                uint pd = Child(tables, physicalRoot, tablePages, pdpt, (uint)(va >> 30), X64PageTableLevel.Pdpte, ref used, conditions);
                if (pd == 0) return Rollback(tables, tablePages);
                uint pt = Child(tables, physicalRoot, tablePages, pd, (uint)((va >> 21) & 511), X64PageTableLevel.Pde, ref used, conditions);
                if (pt == 0 || !Encode(range, page, conditions, out ulong leaf)) return Rollback(tables, tablePages);
                ulong* entry = tables + pt * 512 + ((va >> 12) & 511);
                if (*entry != 0) return Rollback(tables, tablePages);
                *entry = leaf;
            }
        }
        var result = new X64UserAddressSpaceResult
        { Status = X64UserAddressSpaceStatus.Ready, Root = physicalRoot, UsedTables = used, LeafPages = leaves };
        if (used != required || !Audit(ranges, count, tables, physicalRoot, tablePages, conditions, result)) return Rollback(tables, tablePages);
        return result;
    }

    public static bool Audit(X64UserMappingRange* ranges, uint count, ulong* tables, ulong physicalRoot,
        uint tablePages, X64PageTableEncodingPreconditions conditions, X64UserAddressSpaceResult result)
    {
        if (Admit(ranges, count, tables, physicalRoot, tablePages, conditions, out uint required, out ulong expectedLeaves) != X64UserAddressSpaceStatus.Ready ||
            !result.Succeeded || result.Root != physicalRoot || result.UsedTables != required || result.LeafPages != expectedLeaves) return false;
        byte* seen = stackalloc byte[(int)MaximumTables];
        for (uint i = 0; i < MaximumTables; i++) seen[i] = 0;
        seen[0] = 1; uint seenTables = 1; ulong leaves = 0;
        for (uint rootIndex = 0; rootIndex < 512; rootIndex++)
        {
            ulong rootEntry = tables[rootIndex];
            if (rootIndex != 0) { if (rootEntry != 0) return false; continue; }
            if (!AuditChild(rootEntry, physicalRoot, tablePages, X64PageTableLevel.Pml4e, conditions, seen, ref seenTables, out uint pdpt)) return false;
            for (uint pi = 0; pi < 512; pi++)
            {
                ulong pdptEntry = tables[pdpt * 512 + pi];
                if (pdptEntry == 0) continue;
                if (pi >= 4 || !AuditChild(pdptEntry, physicalRoot, tablePages, X64PageTableLevel.Pdpte, conditions, seen, ref seenTables, out uint pd)) return false;
                for (uint di = 0; di < 512; di++)
                {
                    ulong pdEntry = tables[pd * 512 + di];
                    if (pdEntry == 0) continue;
                    if (!AuditChild(pdEntry, physicalRoot, tablePages, X64PageTableLevel.Pde, conditions, seen, ref seenTables, out uint pt)) return false;
                    for (uint ti = 0; ti < 512; ti++)
                    {
                        ulong leaf = tables[pt * 512 + ti];
                        if (leaf == 0) continue;
                        ulong va = ((ulong)pi << 30) | ((ulong)di << 21) | ((ulong)ti << 12);
                        bool matched = false;
                        for (uint r = 0; r <= count; r++)
                        {
                            var range = r == count ? Arena(physicalRoot, tablePages) : ranges[r];
                            if (va < range.VirtualStart || va - range.VirtualStart >= range.Pages * Page) continue;
                            if (!Encode(range, (va - range.VirtualStart) / Page, conditions, out ulong expected) ||
                                (leaf & ~0x60UL) != expected) return false;
                            matched = true; break;
                        }
                        if (!matched) return false;
                        leaves++;
                    }
                }
            }
        }
        if (seenTables != required || leaves != expectedLeaves) return false;
        for (uint page = 1; page < tablePages; page++)
            if (seen[page] == 0) for (uint i = 0; i < 512; i++) if (tables[page * 512 + i] != 0) return false;
        return true;
    }

    public static bool TryReadLeaf(ulong* tables, ulong physicalRoot, uint tablePages, ulong virtualAddress, out ulong leaf)
    {
        leaf = 0;
        if (tables == null || ((ulong)tables & 4095) != 0 || virtualAddress >= End || tablePages < 4 || tablePages > MaximumTables ||
            physicalRoot < Page || (physicalRoot & 4095) != 0 || physicalRoot > End - tablePages * Page ||
            (ulong)tables > ~0UL - tablePages * Page) return false;
        uint page = 0;
        for (int level = 3; level > 0; level--)
        {
            uint slot = (uint)((virtualAddress >> (12 + level * 9)) & 511);
            ulong raw = tables[page * 512 + slot];
            if (raw == 0) return true;
            if ((raw & 0x87) != 7 || !TableIndex(raw, physicalRoot, tablePages, out page)) return false;
        }
        leaf = tables[page * 512 + ((virtualAddress >> 12) & 511)];
        return true; // zero reports an absent guard/hole; authority comes from Audit.
    }

    private static X64UserAddressSpaceStatus Admit(X64UserMappingRange* ranges, uint count, ulong* tables, ulong root,
        uint pages, X64PageTableEncodingPreconditions conditions, out uint required, out ulong leaves)
    {
        required = 0; leaves = 0;
        if (ranges == null || ((ulong)ranges & 7) != 0 || count == 0 || count > MaximumRanges ||
            (ulong)ranges > ~0UL - count * (ulong)sizeof(X64UserMappingRange) || tables == null || ((ulong)tables & 4095) != 0 ||
            pages < 4 || pages > MaximumTables || root < Page || (root & 4095) != 0 || root > End - pages * Page ||
            (ulong)tables > ~0UL - pages * Page || conditions.Validate() != X64PageTableEntryStatus.Success)
            return X64UserAddressSpaceStatus.InvalidInput;
        if (Overlap((ulong)ranges, count * (ulong)sizeof(X64UserMappingRange), (ulong)tables, pages * Page)) return X64UserAddressSpaceStatus.OverlappingRanges;
        byte* slots = stackalloc byte[2048]; byte* directories = stackalloc byte[4];
        for (uint i = 0; i < 2048; i++) slots[i] = 0;
        for (uint i = 0; i < 4; i++) directories[i] = 0;
        required = 2;
        for (uint i = 0; i <= count; i++)
        {
            var range = i == count ? Arena(root, pages) : ranges[i];
            if (range.Pages == 0 || range.Pages > 32768 || range.VirtualStart < Page || range.PhysicalStart < Page ||
                ((range.VirtualStart | range.PhysicalStart) & 4095) != 0 || range.VirtualStart > End - range.Pages * Page ||
                range.PhysicalStart > End - range.Pages * Page) return X64UserAddressSpaceStatus.InvalidRange;
            if (!Encode(range, 0, conditions, out _) || !Encode(range, range.Pages - 1, conditions, out _)) return X64UserAddressSpaceStatus.InvalidEncoding;
            for (uint j = 0; j < i; j++)
                if (Overlap(range.VirtualStart, range.Pages * Page, ranges[j].VirtualStart, ranges[j].Pages * Page) ||
                    Overlap(range.PhysicalStart, range.Pages * Page, ranges[j].PhysicalStart, ranges[j].Pages * Page)) return X64UserAddressSpaceStatus.OverlappingRanges;
            leaves += range.Pages;
            if (leaves > 32768) return X64UserAddressSpaceStatus.InvalidRange;
            uint first = (uint)(range.VirtualStart >> 21), last = (uint)((range.VirtualStart + range.Pages * Page - 1) >> 21);
            for (uint slot = first; slot <= last; slot++)
            {
                if (slots[slot] == 0) { slots[slot] = 1; required++; }
                if (directories[slot / 512] == 0) { directories[slot / 512] = 1; required++; }
            }
        }
        return required <= pages ? X64UserAddressSpaceStatus.Ready : X64UserAddressSpaceStatus.InsufficientTables;
    }

    private static X64UserMappingRange Arena(ulong root, uint pages) => new()
    { VirtualStart = root, PhysicalStart = root, Pages = pages, Protection = PageProtection.Read | PageProtection.Write, Cache = PageCachePolicy.WriteBack, User = false };
    private static bool Encode(X64UserMappingRange range, ulong page, X64PageTableEncodingPreconditions conditions, out ulong raw)
    {
        raw = 0;
        if (PhysicalAddress.TryCreateFromHardwareRegisterBoundary(range.PhysicalStart + page * Page, out var address) != AddressValueStatus.Success) return false;
        var entry = X64PageTableEntry.TryEncode(new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf, address, range.Protection, range.Cache), conditions);
        if (!entry.Succeeded) return false;
        raw = entry.Entry.RawValue | (range.User ? 4UL : 0UL); return true;
    }
    private static bool EncodeTable(ulong addressValue, X64PageTableLevel level, X64PageTableEncodingPreconditions conditions, out ulong raw)
    {
        raw = 0;
        if (PhysicalAddress.TryCreateFromHardwareRegisterBoundary(addressValue, out var address) != AddressValueStatus.Success) return false;
        var entry = X64PageTableEntry.TryEncode(new(level, X64PageTableEntryKind.TablePointer, address, PageProtection.None, PageCachePolicy.WriteBack), conditions);
        if (!entry.Succeeded) return false;
        raw = entry.Entry.RawValue | 4UL; return true;
    }
    private static uint Child(ulong* tables, ulong root, uint pages, uint parent, uint slot,
        X64PageTableLevel level, ref uint used, X64PageTableEncodingPreconditions conditions)
    {
        ulong* entry = tables + parent * 512 + slot;
        if (*entry != 0) return TableIndex(*entry, root, pages, out uint existing) ? existing : 0;
        if (used >= pages || !EncodeTable(root + used * Page, level, conditions, out ulong raw)) return 0;
        uint child = used++; *entry = raw; return child;
    }
    private static bool AuditChild(ulong raw, ulong root, uint pages, X64PageTableLevel level,
        X64PageTableEncodingPreconditions conditions, byte* seen, ref uint count, out uint child)
    {
        child = 0;
        if (!TableIndex(raw, root, pages, out child) || seen[child] != 0 ||
            !EncodeTable(root + child * Page, level, conditions, out ulong expected) || (raw & ~0x20UL) != expected) return false;
        seen[child] = 1; count++; return true;
    }
    private static bool TableIndex(ulong raw, ulong root, uint pages, out uint index)
    {
        index = 0; ulong address = raw & AddressMask;
        if (address <= root || address - root >= pages * Page) return false;
        index = (uint)((address - root) / Page); return true;
    }
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a < b + bBytes && b < a + aBytes;
    public static bool Clear(ulong* tables, uint pages)
    {
        if (tables == null || ((ulong)tables & 4095) != 0 || pages == 0 || pages > MaximumTables || (ulong)tables > ~0UL - pages * Page) return false;
        for (uint i = 0; i < pages * 512; i++) tables[i] = 0;
        return true;
    }
    private static X64UserAddressSpaceResult Failure(X64UserAddressSpaceStatus status) => new() { Status = status };
    private static X64UserAddressSpaceResult Rollback(ulong* tables, uint pages)
    { Clear(tables, pages); return Failure(X64UserAddressSpaceStatus.AuditFailed); }
}
