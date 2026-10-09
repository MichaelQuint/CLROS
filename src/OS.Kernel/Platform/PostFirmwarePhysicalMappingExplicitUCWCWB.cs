using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;

public enum PhysicalMappingUsage : byte
{
    Uninitialized = 0,
    Normal = 1,
    Device = 2,
    Framebuffer = 3
}

public enum PhysicalMappingPreparationStatus : byte
{
    Uninitialized = 0,
    Prepared = 1,
    InvalidUsage = 2,
    InvalidRange = 3,
    InvalidInheritedEntry = 4,
    RangeOutsideLargePage = 5,
    InvalidTableAddress = 6,
    TableOverlapsMappedRange = 7,
    InvalidDestination = 8,
    UnsupportedCachePolicy = 9
}

public readonly struct PhysicalMappingPreparationResult
{
    internal PhysicalMappingPreparationResult(
        PhysicalMappingPreparationStatus status,
        ulong replacementPde = 0,
        X64PageTableEntryStatus entryStatus = X64PageTableEntryStatus.Uninitialized)
    {
        Status = status;
        ReplacementPde = replacementPde;
        EntryStatus = entryStatus;
    }

    public PhysicalMappingPreparationStatus Status { get; }
    public bool IsPrepared => Status == PhysicalMappingPreparationStatus.Prepared;
    public ulong ReplacementPde { get; }
    public X64PageTableEntryStatus EntryStatus { get; }
}

/// <summary>
/// Prepares detached page-table bytes; it never publishes a mapping. The caller
/// must own the destination exclusively and keep it separate from live tables.
/// Alias admission, MTRR/PAT compatibility, ownership, publication and cache/TLB
/// maintenance must be completed before these bytes can become a live mapping.
/// </summary>
public static class PostFirmwarePhysicalMappingExplicitUCWCWB
{
    public const uint EntriesPerTable = 512;
    public const ulong PageBytes = 4096;
    public const ulong LargePageBytes = EntriesPerTable * PageBytes;

    public static PageCachePolicy GetCachePolicy(PhysicalMappingUsage usage)
    {
        return usage switch
        {
            PhysicalMappingUsage.Normal => PageCachePolicy.WriteBack,
            PhysicalMappingUsage.Device => PageCachePolicy.Uncached,
            PhysicalMappingUsage.Framebuffer => PageCachePolicy.WriteCombining,
            _ => PageCachePolicy.Uninitialized
        };
    }

    /// <summary>
    /// Splits one admitted 2 MiB leaf into 512 detached 4 KiB entries, changing
    /// cache policy only inside the requested physical range. Permissions and
    /// physical translations are preserved. The large leaf's A/D observations
    /// conservatively apply to every child. Failure leaves destination untouched.
    /// Only the strict, existing x64 entry decoder's admitted subset is supported.
    /// </summary>
    public static unsafe PhysicalMappingPreparationResult PrepareTwoMiBSplit(
        ulong inheritedPde,
        PhysicalAddress physicalStart,
        ulong byteLength,
        PhysicalMappingUsage usage,
        PhysicalAddress tablePhysicalAddress,
        ulong* destination,
        uint destinationEntryCapacity,
        X64PageTableEncodingPreconditions preconditions)
    {
        PageCachePolicy policy = GetCachePolicy(usage);
        if (policy == PageCachePolicy.Uninitialized)
        {
            return new(PhysicalMappingPreparationStatus.InvalidUsage);
        }

        if (physicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start)
                != AddressValueStatus.Success
            || (start & (PageBytes - 1)) != 0
            || byteLength == 0
            || (byteLength & (PageBytes - 1)) != 0
            || byteLength > ulong.MaxValue - start)
        {
            return new(PhysicalMappingPreparationStatus.InvalidRange);
        }

        X64PageTableEntryResult inherited = X64PageTableEntry.TryDecode(
            X64PageTableLevel.Pde, inheritedPde, preconditions);
        if (!inherited.Succeeded || !inherited.IsLargePage)
        {
            return new(PhysicalMappingPreparationStatus.InvalidInheritedEntry,
                entryStatus: inherited.Status);
        }

        inherited.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong baseAddress);
        // The decoder admits at most 52-bit, 2 MiB aligned physical addresses.
        ulong end = start + byteLength;
        ulong largeEnd = baseAddress + LargePageBytes;
        if (start < baseAddress || end > largeEnd)
        {
            return new(PhysicalMappingPreparationStatus.RangeOutsideLargePage);
        }

        X64PageTableEntryResult table = X64PageTableEntry.TryEncode(
            new(X64PageTableLevel.Pde, X64PageTableEntryKind.TablePointer,
                tablePhysicalAddress, PageProtection.None, PageCachePolicy.WriteBack),
            preconditions);
        if (!table.Succeeded)
        {
            return new(PhysicalMappingPreparationStatus.InvalidTableAddress,
                entryStatus: table.Status);
        }

        tablePhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong tableAddress);
        if (tableAddress >= baseAddress && tableAddress < largeEnd)
        {
            return new(PhysicalMappingPreparationStatus.TableOverlapsMappedRange);
        }

        ulong destinationAddress = (ulong)destination;
        if (destination == null || (destinationAddress & (sizeof(ulong) - 1)) != 0
            || destinationEntryCapacity < EntriesPerTable
            || destinationAddress > ulong.MaxValue - PageBytes)
        {
            return new(PhysicalMappingPreparationStatus.InvalidDestination);
        }

        X64PageTableEntryResult original = X64PageTableEntry.TryEncode(
            new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf,
                inherited.PhysicalAddress, inherited.Protection, inherited.CachePolicy),
            preconditions);
        X64PageTableEntryResult selected = X64PageTableEntry.TryEncode(
            new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf,
                inherited.PhysicalAddress, inherited.Protection, policy),
            preconditions);
        if (!original.Succeeded || !selected.Succeeded)
        {
            return new(PhysicalMappingPreparationStatus.UnsupportedCachePolicy,
                entryStatus: !original.Succeeded ? original.Status : selected.Status);
        }

        // Encoding the aligned base proves every child: offsets use only the
        // previously zero address bits 12..20, below the physical-width limit.
        // Re-encoding is required because PAT moves from bit 12 to bit 7.
        ulong hardwareState = inheritedPde & ((1UL << 5) | (1UL << 6));
        uint firstPage = (uint)((start - baseAddress) / PageBytes);
        uint endPage = (uint)((end - baseAddress) / PageBytes);
        for (uint index = 0; index < EntriesPerTable; index++)
        {
            ulong raw = index >= firstPage && index < endPage
                ? selected.Entry.RawValue : original.Entry.RawValue;
            destination[index] = raw | (index * PageBytes) | hardwareState;
        }

        return new(PhysicalMappingPreparationStatus.Prepared,
            table.Entry.RawValue | (inheritedPde & (1UL << 5)),
            X64PageTableEntryStatus.Success);
    }
}
