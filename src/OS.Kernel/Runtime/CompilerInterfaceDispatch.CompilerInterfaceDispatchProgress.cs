using OS.Boot;

namespace OS.Kernel.Runtime;
public struct CompilerInterfaceDispatchProgress
{
    public uint CellCount, CacheCount, MetadataWords;
    public ulong Resolutions, CacheHits;
}
