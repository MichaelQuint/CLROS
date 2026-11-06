namespace OS.Kernel.Memory;
/// <summary>
/// Marks a page-aligned virtual interval whose leaf entry must not be removed
/// or rewritten by this mutation boundary.
/// </summary>

public readonly struct X64EssentialMappingRange
{
    public const ulong StructureSize = 32;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.VirtualAddress _virtualStart;
    private readonly OS.Boot.VirtualAddress _virtualEndExclusive;
    private readonly ulong _pageCount;
    private X64EssentialMappingRange(OS.Boot.VirtualAddress virtualStart, OS.Boot.VirtualAddress virtualEndExclusive, ulong pageCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _pageCount = pageCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public OS.Boot.VirtualAddress VirtualStart => _virtualStart;
    public OS.Boot.VirtualAddress VirtualEndExclusive => _virtualEndExclusive;
    public ulong PageCount => _pageCount;

    public static X64PageMapStatus TryCreate(OS.Boot.VirtualAddress virtualStart, ulong pageCount, out X64EssentialMappingRange range)
    {
        range = default;
        if (!virtualStart.IsValid || !virtualStart.IsAligned(X64PageMap.PageSize) || pageCount == 0 || pageCount > X64PageMap.MaximumEssentialRangePages || pageCount > ~0UL / X64PageMap.PageSize)
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        if (virtualStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawStart) != OS.Boot.AddressValueStatus.Success || rawStart == 0 || rawStart >= X64PageMap.LowCanonicalEndExclusive)
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        ulong byteLength = pageCount * X64PageMap.PageSize;
        if (virtualStart.TryGetEndExclusive(byteLength, out OS.Boot.VirtualAddress endExclusive) != OS.Boot.AddressValueStatus.Success || endExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEnd) != OS.Boot.AddressValueStatus.Success || rawEnd > X64PageMap.LowCanonicalEndExclusive || rawEnd <= rawStart)
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        range = new X64EssentialMappingRange(virtualStart, endExclusive, pageCount);
        return X64PageMapStatus.Success;
    }

    internal X64PageMapStatus Validate()
    {
        if (!IsInitialized || TryCreate(_virtualStart, _pageCount, out X64EssentialMappingRange recreated) != X64PageMapStatus.Success || !recreated.VirtualEndExclusive.Equals(_virtualEndExclusive))
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        return X64PageMapStatus.Success;
    }
}
