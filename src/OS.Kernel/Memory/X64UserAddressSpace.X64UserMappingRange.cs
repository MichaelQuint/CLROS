using OS.Boot;

namespace OS.Kernel.Memory;

public struct X64UserMappingRange
{
    public ulong VirtualStart, PhysicalStart, Pages;
    public PageProtection Protection;
    public PageCachePolicy Cache;
    public bool User;
}
