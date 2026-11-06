namespace OS.Kernel.Boot;
/// <summary>
/// Caller-owned, value-only description of one required or guarded physical
/// interval. Both ends and both geometry values are carried so the boundary
/// can reject disagreement instead of silently normalizing it.
/// </summary>

public readonly struct X64BootIdentityMappingRequirement
{
    public const ulong StructureSize = 40;
    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly OS.Boot.PhysicalAddress _physicalEndExclusive;
    private readonly ulong _pageCount;
    private readonly ulong _byteLength;
    private readonly byte _isInitialized;
    private readonly X64BootIdentityMappingPurpose _purpose;
    private readonly OS.Kernel.Memory.PageProtection _protection;
    private readonly OS.Kernel.Memory.PageCachePolicy _cachePolicy;
    private readonly uint _reserved;
    public X64BootIdentityMappingRequirement(X64BootIdentityMappingPurpose purpose, OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, ulong pageCount, ulong byteLength, OS.Kernel.Memory.PageProtection protection, OS.Kernel.Memory.PageCachePolicy cachePolicy)
    {
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _pageCount = pageCount;
        _byteLength = byteLength;
        _isInitialized = 1;
        _purpose = purpose;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _reserved = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved == 0;
    public X64BootIdentityMappingPurpose Purpose => _purpose;
    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;
    public OS.Boot.PhysicalAddress PhysicalEndExclusive => _physicalEndExclusive;
    public ulong PageCount => _pageCount;
    public ulong ByteLength => _byteLength;
    public OS.Kernel.Memory.PageProtection Protection => _protection;
    public OS.Kernel.Memory.PageCachePolicy CachePolicy => _cachePolicy;
    internal bool HasCanonicalState => IsInitialized;
}
