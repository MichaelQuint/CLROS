using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Diagnostics;
/// <summary>
/// Caller-supplied mode values are not processor observations made by this API.
/// The physical arena and its writable host backing pointer are deliberately distinct.
/// </summary>

public readonly struct SeparatePageFaultPageTableRequest
{
    private readonly byte _initialized;
    public SeparatePageFaultPageTableRequest(PhysicalAddress arenaStart, ulong arenaPageCount, X64PageTableEncodingPreconditions preconditions)
    {
        _initialized = 1;
        ArenaStart = arenaStart;
        ArenaPageCount = arenaPageCount;
        Preconditions = preconditions;
    }

    public bool IsInitialized => _initialized == 1;
    public PhysicalAddress ArenaStart
    {
        get;
    }
    public ulong ArenaPageCount
    {
        get;
    }
    public X64PageTableEncodingPreconditions Preconditions
    {
        get;
    }
}
