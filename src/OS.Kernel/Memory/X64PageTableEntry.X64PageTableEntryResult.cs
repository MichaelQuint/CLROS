namespace OS.Kernel.Memory;
/// <summary>
/// Status-coded result for one encoded or decoded entry.
/// </summary>

public readonly struct X64PageTableEntryResult
{
    public const ulong StructureSize = 40;
    private readonly X64PageTableEntry _entry;
    private readonly OS.Boot.PhysicalAddress _physicalAddress;
    private readonly ulong _pageSizeBytes;
    private readonly byte _isInitialized;
    private readonly X64PageTableEntryStatus _status;
    private readonly X64PageTableLevel _level;
    private readonly X64PageTableEntryKind _kind;
    private readonly PageProtection _protection;
    private readonly PageCachePolicy _cachePolicy;
    private readonly byte _patIndex;
    private readonly byte _accessed;
    private readonly byte _dirty;
    private X64PageTableEntryResult(X64PageTableEntryStatus status, X64PageTableEntry entry, X64PageTableLevel level, X64PageTableEntryKind kind, OS.Boot.PhysicalAddress physicalAddress, PageProtection protection, PageCachePolicy cachePolicy, ulong pageSizeBytes, byte patIndex, bool accessed, bool dirty)
    {
        _entry = entry;
        _physicalAddress = physicalAddress;
        _pageSizeBytes = pageSizeBytes;
        _isInitialized = 1;
        _status = status;
        _level = level;
        _kind = kind;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _patIndex = patIndex;
        _accessed = accessed ? (byte)1 : (byte)0;
        _dirty = dirty ? (byte)1 : (byte)0;
    }

    public bool IsInitialized => _isInitialized == 1;
    public X64PageTableEntryStatus Status => _status;
    public bool Succeeded => IsInitialized && _status == X64PageTableEntryStatus.Success;
    public X64PageTableEntry Entry => _entry;
    public X64PageTableLevel Level => _level;
    public X64PageTableEntryKind Kind => _kind;
    public OS.Boot.PhysicalAddress PhysicalAddress => _physicalAddress;
    public PageProtection Protection => _protection;
    public PageCachePolicy CachePolicy => _cachePolicy;
    public ulong PageSizeBytes => _pageSizeBytes;
    public byte PatIndex => _patIndex;
    public bool IsPresent => Succeeded && _kind != X64PageTableEntryKind.Unmapped;
    public bool IsLargePage => Succeeded && _kind == X64PageTableEntryKind.Leaf && (_level == X64PageTableLevel.Pde || _level == X64PageTableLevel.Pdpte);
    public bool Accessed => _accessed == 1;
    public bool Dirty => _dirty == 1;

    internal static X64PageTableEntryResult Success(X64PageTableEntry entry, X64PageTableLevel level, X64PageTableEntryKind kind, OS.Boot.PhysicalAddress physicalAddress, PageProtection protection, PageCachePolicy cachePolicy, ulong pageSizeBytes, byte patIndex, bool accessed, bool dirty)
    {
        return new X64PageTableEntryResult(X64PageTableEntryStatus.Success, entry, level, kind, physicalAddress, protection, cachePolicy, pageSizeBytes, patIndex, accessed, dirty);
    }

    internal static X64PageTableEntryResult Fail(X64PageTableEntryStatus status)
    {
        return new X64PageTableEntryResult(status, default, X64PageTableLevel.Uninitialized, X64PageTableEntryKind.Uninitialized, default, PageProtection.None, PageCachePolicy.Uninitialized, 0, X64PatMappingToken.UnsupportedIndex, accessed: false, dirty: false);
    }
}
