namespace OS.Kernel.Memory;
/// <summary>
/// Immutable result for one successful or rejected caller-owned leaf rewrite.
/// </summary>

public readonly struct X64PageMapResult
{
    public const ulong StructureSize = 264;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64PageMapStatus _status;
    private readonly X64PageTableEntryStatus _entryStatus;
    private readonly X64TlbInvalidationStatus _invalidationStatus;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly uint _reserved2;
    private readonly X64PageMapRequest _request;
    private readonly ulong _pageImageCount;
    private readonly ulong _pageImageCapacity;
    private readonly OS.Boot.PhysicalAddress _pageImagePhysicalStart;
    private readonly OS.Boot.PhysicalAddress _pageImagePhysicalEndExclusive;
    private readonly ulong _entryCount;
    private readonly ulong _entryCapacity;
    private readonly ulong _essentialRangeCount;
    private readonly ulong _essentialRangeCapacity;
    private readonly ulong _rootPageIndex;
    private readonly ulong _targetPageIndex;
    private readonly ulong _targetEntryIndex;
    private readonly ulong _invalidPageImageIndex;
    private readonly ulong _invalidEssentialRangeIndex;
    private readonly ulong _changedEntryCount;
    private readonly OS.Boot.VirtualAddress _changedVirtualStart;
    private readonly OS.Boot.VirtualAddress _changedVirtualEndExclusive;
    private readonly X64PageTableEntry _previousEntry;
    private readonly X64PageTableEntry _replacementEntry;
    private readonly X64TlbInvalidationRange _invalidationRange;
    private X64PageMapResult(X64PageMapStatus status, X64PageTableEntryStatus entryStatus, X64TlbInvalidationStatus invalidationStatus, X64PageMapRequest request, ulong pageImageCount, ulong pageImageCapacity, OS.Boot.PhysicalAddress pageImagePhysicalStart, OS.Boot.PhysicalAddress pageImagePhysicalEndExclusive, ulong entryCount, ulong entryCapacity, ulong essentialRangeCount, ulong essentialRangeCapacity, ulong rootPageIndex, ulong targetPageIndex, ulong targetEntryIndex, ulong invalidPageImageIndex, ulong invalidEssentialRangeIndex, ulong changedEntryCount, OS.Boot.VirtualAddress changedVirtualStart, OS.Boot.VirtualAddress changedVirtualEndExclusive, X64PageTableEntry previousEntry, X64PageTableEntry replacementEntry, X64TlbInvalidationRange invalidationRange)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _entryStatus = entryStatus;
        _invalidationStatus = invalidationStatus;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _request = request;
        _pageImageCount = pageImageCount;
        _pageImageCapacity = pageImageCapacity;
        _pageImagePhysicalStart = pageImagePhysicalStart;
        _pageImagePhysicalEndExclusive = pageImagePhysicalEndExclusive;
        _entryCount = entryCount;
        _entryCapacity = entryCapacity;
        _essentialRangeCount = essentialRangeCount;
        _essentialRangeCapacity = essentialRangeCapacity;
        _rootPageIndex = rootPageIndex;
        _targetPageIndex = targetPageIndex;
        _targetEntryIndex = targetEntryIndex;
        _invalidPageImageIndex = invalidPageImageIndex;
        _invalidEssentialRangeIndex = invalidEssentialRangeIndex;
        _changedEntryCount = changedEntryCount;
        _changedVirtualStart = changedVirtualStart;
        _changedVirtualEndExclusive = changedVirtualEndExclusive;
        _previousEntry = previousEntry;
        _replacementEntry = replacementEntry;
        _invalidationRange = invalidationRange;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0;
    public X64PageMapStatus Status => _status;
    public X64PageTableEntryStatus EntryStatus => _entryStatus;
    public X64TlbInvalidationStatus InvalidationStatus => _invalidationStatus;
    public X64PageMapRequest Request => _request;
    public ulong PageImageCount => _pageImageCount;
    public ulong PageImageCapacity => _pageImageCapacity;
    public OS.Boot.PhysicalAddress PageImagePhysicalStart => _pageImagePhysicalStart;
    public OS.Boot.PhysicalAddress PageImagePhysicalEndExclusive => _pageImagePhysicalEndExclusive;
    public ulong EntryCount => _entryCount;
    public ulong EntryCapacity => _entryCapacity;
    public ulong EssentialRangeCount => _essentialRangeCount;
    public ulong EssentialRangeCapacity => _essentialRangeCapacity;
    public ulong RootPageIndex => _rootPageIndex;
    public ulong TargetPageIndex => _targetPageIndex;
    public ulong TargetEntryIndex => _targetEntryIndex;
    public ulong InvalidPageImageIndex => _invalidPageImageIndex;
    public ulong InvalidEssentialRangeIndex => _invalidEssentialRangeIndex;
    public ulong ChangedEntryCount => _changedEntryCount;
    public OS.Boot.VirtualAddress ChangedVirtualStart => _changedVirtualStart;
    public OS.Boot.VirtualAddress ChangedVirtualEndExclusive => _changedVirtualEndExclusive;
    public X64PageTableEntry PreviousEntry => _previousEntry;
    public X64PageTableEntry ReplacementEntry => _replacementEntry;
    public X64TlbInvalidationRange InvalidationRange => _invalidationRange;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized || _status != X64PageMapStatus.Success || _entryStatus != X64PageTableEntryStatus.Success || _invalidationStatus != X64TlbInvalidationStatus.Success || _request.Validate() != X64PageMapStatus.Success || _pageImageCount == 0 || _pageImageCount > _pageImageCapacity || _pageImageCapacity > X64PageMap.MaximumPageImages || _pageImagePhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPageImageStart) != OS.Boot.AddressValueStatus.Success || _pageImagePhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPageImageEnd) != OS.Boot.AddressValueStatus.Success || !_pageImagePhysicalStart.IsAligned(X64PageMap.PageSize) || !_pageImagePhysicalEndExclusive.IsAligned(X64PageMap.PageSize) || rawPageImageEnd <= rawPageImageStart || _pageImageCount > ~0UL / X64PageMap.PageSize || rawPageImageEnd - rawPageImageStart < _pageImageCount * X64PageMap.PageSize || _request.RootTable.TryGetRawValueForHardwareRegisterBoundary(out ulong rawRootTable) != OS.Boot.AddressValueStatus.Success || rawRootTable < rawPageImageStart || rawRootTable > rawPageImageEnd - X64PageMap.PageSize || rawPageImageEnd > (1UL << _request.Preconditions.PhysicalAddressWidth) || _entryCount != _pageImageCount * X64PageTableImagePage.EntryCountPerPage || _entryCount > _entryCapacity || _entryCapacity > X64PageMap.MaximumEntryCount || _essentialRangeCount > _essentialRangeCapacity || _essentialRangeCapacity > X64PageMap.MaximumEssentialRangeCount || _rootPageIndex >= _pageImageCount || _targetPageIndex >= _pageImageCount || _targetEntryIndex >= _entryCount || _invalidPageImageIndex != NoIndex || _invalidEssentialRangeIndex != NoIndex || _changedEntryCount != 1 || _previousEntry.RawValue == _replacementEntry.RawValue || _invalidationRange.Validate() != X64TlbInvalidationStatus.Success || _invalidationRange.PageCount != 1 || _invalidationRange.ByteLength != X64PageMap.PageSize || !_invalidationRange.VirtualStart.Equals(_request.VirtualAddress) || !_changedVirtualStart.Equals(_request.VirtualAddress) || !_changedVirtualEndExclusive.Equals(_invalidationRange.VirtualEndExclusive))
            {
                return false;
            }

            if (_request.Operation == X64PageMapOperation.Map)
            {
                return _previousEntry.RawValue == 0 && _replacementEntry.RawValue != 0;
            }

            if (_request.Operation == X64PageMapOperation.Unmap)
            {
                return _previousEntry.RawValue != 0 && _replacementEntry.RawValue == 0;
            }

            return _request.Operation == X64PageMapOperation.Protect && _previousEntry.RawValue != 0 && _replacementEntry.RawValue != 0;
        }
    }

    internal static X64PageMapResult Success(X64PageMapRequest request, ulong pageImageCount, ulong pageImageCapacity, OS.Boot.PhysicalAddress pageImagePhysicalStart, OS.Boot.PhysicalAddress pageImagePhysicalEndExclusive, ulong entryCount, ulong entryCapacity, ulong essentialRangeCount, ulong essentialRangeCapacity, ulong rootPageIndex, ulong targetPageIndex, ulong targetEntryIndex, X64PageTableEntry previousEntry, X64PageTableEntry replacementEntry, X64TlbInvalidationRange invalidationRange)
    {
        return new X64PageMapResult(X64PageMapStatus.Success, X64PageTableEntryStatus.Success, X64TlbInvalidationStatus.Success, request, pageImageCount, pageImageCapacity, pageImagePhysicalStart, pageImagePhysicalEndExclusive, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex, targetPageIndex, targetEntryIndex, NoIndex, NoIndex, 1, request.VirtualAddress, invalidationRange.VirtualEndExclusive, previousEntry, replacementEntry, invalidationRange);
    }

    internal static X64PageMapResult Fail(X64PageMapStatus status, X64PageMapRequest request, ulong pageImageCount, ulong pageImageCapacity, ulong entryCount, ulong entryCapacity, ulong essentialRangeCount, ulong essentialRangeCapacity, X64PageTableEntryStatus entryStatus = X64PageTableEntryStatus.Uninitialized, X64TlbInvalidationStatus invalidationStatus = X64TlbInvalidationStatus.Uninitialized, ulong rootPageIndex = NoIndex, ulong targetPageIndex = NoIndex, ulong targetEntryIndex = NoIndex, ulong invalidPageImageIndex = NoIndex, ulong invalidEssentialRangeIndex = NoIndex)
    {
        return new X64PageMapResult(status, entryStatus, invalidationStatus, request, pageImageCount, pageImageCapacity, default, default, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex, targetPageIndex, targetEntryIndex, invalidPageImageIndex, invalidEssentialRangeIndex, 0, default, default, default, default, default);
    }
}
