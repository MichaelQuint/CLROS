#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// A genuine compiler-described fixed class with one reference series. Its
// constructor and accessors run only after the original allocator is bound.
internal sealed class GcCollectorNode
{
    private GcCollectorNode child;
    private readonly ulong tag;
    internal GcCollectorNode(ulong value)
    {
        RhpNewFastAllocation.ConstructorEntered();
        tag = value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void SetChild(GcCollectorNode value) => child = value;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal GcCollectorNode GetChild() => child;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ulong GetTag() => tag;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ref GcCollectorNode ChildAddress() => ref child;
}

#pragma warning disable CS0649 // Written through bounded sequential POD slots.

#pragma warning restore CS0649

#endif
