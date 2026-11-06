namespace OS.Kernel.Memory;
/// <summary>
/// Unmanaged request for one canonical x64 page-table entry.
/// </summary>

public readonly struct X64PageTableEntryRequest
{
    public const ulong StructureSize = 16;
    private readonly OS.Boot.PhysicalAddress _physicalAddress;
    private readonly byte _isInitialized;
    private readonly X64PageTableLevel _level;
    private readonly X64PageTableEntryKind _kind;
    private readonly PageProtection _protection;
    private readonly PageCachePolicy _cachePolicy;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    public X64PageTableEntryRequest(X64PageTableLevel level, X64PageTableEntryKind kind, OS.Boot.PhysicalAddress physicalAddress, PageProtection protection, PageCachePolicy cachePolicy)
    {
        _physicalAddress = physicalAddress;
        _isInitialized = 1;
        _level = level;
        _kind = kind;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized => _isInitialized == 1;
    public X64PageTableLevel Level => _level;
    public X64PageTableEntryKind Kind => _kind;
    public OS.Boot.PhysicalAddress PhysicalAddress => _physicalAddress;
    public PageProtection Protection => _protection;
    public PageCachePolicy CachePolicy => _cachePolicy;

    public X64PageTableEntryStatus Validate()
    {
        if (_isInitialized == 0)
        {
            return X64PageTableEntryStatus.RequestNotInitialized;
        }

        return _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 ? X64PageTableEntryStatus.Success : X64PageTableEntryStatus.InvalidRequestState;
    }
}
