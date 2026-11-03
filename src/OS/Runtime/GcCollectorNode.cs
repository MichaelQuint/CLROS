#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

#if M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE

#endif

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

#if M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE

#endif

#if M04_FILTERED_CATCH_PROBE

#elif M04_CONSTRUCTOR_FAILURE_PROBE

#elif M04_NORMAL_FINALLY_PROBE

#elif M04_FRAME_DISPATCH_PROBE

#elif M04_UNHANDLED_EXCEPTION_PROBE

#elif M04_NESTED_FINALLY_CATCH_PROBE

#elif M04_RETHROW_PROBE

#elif M04_SINGLE_FINALLY_CATCH_PROBE

#elif M04_SIMPLE_THROW_CATCH_PROBE

#endif
#if M04_INTERFACE_DISPATCH_PROBE

#endif
#if M04_SEEDED_COLLECTOR_CHURN_PROBE

#endif

#if M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE || M04_NESTED_FINALLY_CATCH_PROBE || M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_FILTERED_CATCH_PROBE || M04_NORMAL_FINALLY_PROBE
#if M04_CONSTRUCTOR_FAILURE_PROBE

#endif

#endif
#if M04_INTERFACE_DISPATCH_PROBE

#endif

#if M04_CONSTRUCTOR_FAILURE_PROBE

#endif

#pragma warning disable CS0649 // Written through bounded sequential POD slots.

#pragma warning restore CS0649

#endif
