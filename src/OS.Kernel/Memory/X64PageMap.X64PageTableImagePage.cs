namespace OS.Kernel.Memory;
/// <summary>
/// Locates one 4 KiB table page inside a separate flat caller-owned array of
/// raw entry values. No pointer is retained in this POD.
/// </summary>

public readonly struct X64PageTableImagePage
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;
    public const ulong EntryCountPerPage = 512;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly ulong _entryStartIndex;
    private X64PageTableImagePage(OS.Boot.PhysicalAddress physicalStart, ulong entryStartIndex)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _physicalStart = physicalStart;
        _entryStartIndex = entryStartIndex;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;
    public ulong EntryStartIndex => _entryStartIndex;

    public static X64PageMapStatus TryCreate(OS.Boot.PhysicalAddress physicalStart, ulong entryStartIndex, out X64PageTableImagePage page)
    {
        page = default;
        if (!physicalStart.IsValid || !physicalStart.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        if ((entryStartIndex & (EntryCountPerPage - 1)) != 0 || entryStartIndex > ~0UL - EntryCountPerPage)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        page = new X64PageTableImagePage(physicalStart, entryStartIndex);
        return X64PageMapStatus.Success;
    }

    internal X64PageMapStatus Validate(ulong ordinal, ulong entryCount, byte physicalAddressWidth)
    {
        if (!IsInitialized || !_physicalStart.IsValid || !_physicalStart.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        if (ordinal > ~0UL / EntryCountPerPage || _entryStartIndex != ordinal * EntryCountPerPage || _entryStartIndex > entryCount || entryCount - _entryStartIndex < EntryCountPerPage)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        if (_physicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPhysical) != OS.Boot.AddressValueStatus.Success)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        ulong physicalLimit = 1UL << physicalAddressWidth;
        if (rawPhysical >= physicalLimit || rawPhysical > physicalLimit - X64PageMap.PageSize)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        return X64PageMapStatus.Success;
    }
}
