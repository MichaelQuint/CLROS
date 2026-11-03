using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Diagnostics;
/// <summary>One caller-declared identity interval; no allocation or lifetime authority.</summary>

public readonly struct SeparatePageFaultMappingRange
{
    private readonly byte _initialized;
    public SeparatePageFaultMappingRange(ulong start, ulong pageCount, PageProtection protection, PageCachePolicy cachePolicy = PageCachePolicy.WriteBack)
    {
        _initialized = 1;
        Start = start;
        PageCount = pageCount;
        Protection = protection;
        CachePolicy = cachePolicy;
    }

    public bool IsInitialized => _initialized == 1;
    public ulong Start
    {
        get;
    }
    public ulong PageCount
    {
        get;
    }
    public PageProtection Protection
    {
        get;
    }
    public PageCachePolicy CachePolicy
    {
        get;
    }
}
#if M16_025_PROBE

#else
#endif
#if M10_004_PROBE

#else
#endif
