using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>Optional original retained GC-static root inputs.</summary>

public unsafe struct CollectorStaticRootAccess
{
    public byte* Owner;
    public ulong Capacity;
    public GCStaticReadableWindow Cells;
    public ulong SystemObjectMethodTable;
}
