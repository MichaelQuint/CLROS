using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;
public readonly struct PhysicalMappingPreparationResult
{
    internal PhysicalMappingPreparationResult(PhysicalMappingPreparationStatus status, ulong replacementPde = 0, X64PageTableEntryStatus entryStatus = X64PageTableEntryStatus.Uninitialized, uint parentEntryIndex = 0xffffffffU, uint childTablePageIndex = 0xffffffffU, ulong originalPde = 0)
    {
        Status = status;
        ReplacementPde = replacementPde;
        EntryStatus = entryStatus;
        ParentEntryIndex = parentEntryIndex;
        ChildTablePageIndex = childTablePageIndex;
        OriginalPde = originalPde;
    }

    public PhysicalMappingPreparationStatus Status { get; }
    public bool IsPrepared => Status == PhysicalMappingPreparationStatus.Prepared;
    public ulong ReplacementPde { get; }
    public X64PageTableEntryStatus EntryStatus { get; }
    public uint ParentEntryIndex { get; }
    public uint ChildTablePageIndex { get; }
    public ulong OriginalPde { get; }
}
