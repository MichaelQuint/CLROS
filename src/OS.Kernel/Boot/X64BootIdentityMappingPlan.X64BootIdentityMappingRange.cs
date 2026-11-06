namespace OS.Kernel.Boot;
/// <summary>
/// One normalized 4 KiB identity-mapping interval. This is a plan record only;
/// constructing it does not allocate tables or change a live mapping.
/// </summary>

public readonly struct X64BootIdentityMappingRange
{
    public const ulong StructureSize = 64;
    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly OS.Boot.PhysicalAddress _physicalEndExclusive;
    private readonly OS.Boot.VirtualAddress _virtualStart;
    private readonly OS.Boot.VirtualAddress _virtualEndExclusive;
    private readonly ulong _pageCount;
    private readonly ulong _byteLength;
    private readonly ulong _contributingPurposeMask;
    private readonly byte _isInitialized;
    private readonly OS.Kernel.Memory.PageProtection _protection;
    private readonly OS.Kernel.Memory.PageCachePolicy _cachePolicy;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    internal X64BootIdentityMappingRange(OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, OS.Boot.VirtualAddress virtualStart, OS.Boot.VirtualAddress virtualEndExclusive, ulong pageCount, ulong byteLength, ulong contributingPurposeMask, OS.Kernel.Memory.PageProtection protection, OS.Kernel.Memory.PageCachePolicy cachePolicy)
    {
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _pageCount = pageCount;
        _byteLength = byteLength;
        _contributingPurposeMask = contributingPurposeMask;
        _isInitialized = 1;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0;
    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;
    public OS.Boot.PhysicalAddress PhysicalEndExclusive => _physicalEndExclusive;
    public OS.Boot.VirtualAddress VirtualStart => _virtualStart;
    public OS.Boot.VirtualAddress VirtualEndExclusive => _virtualEndExclusive;
    public ulong PageCount => _pageCount;
    public ulong ByteLength => _byteLength;
    public ulong ContributingPurposeMask => _contributingPurposeMask;
    public OS.Kernel.Memory.PageProtection Protection => _protection;
    public OS.Kernel.Memory.PageCachePolicy CachePolicy => _cachePolicy;
    internal bool HasCanonicalState => IsInitialized;
}
