using OS.Kernel.Runtime;

namespace OS.Runtime;
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#endif
#pragma warning disable CS0649 // Assigned by the enabled profile and mapped core tests.

#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE

#pragma warning disable CS0649
internal sealed class ReferenceCell
{
    internal object Reference;
    internal ulong Tag;
    internal ReferencePair Payload;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal ReferenceCell(ulong tag)
    {
        RhpNewFastAllocation.ConstructorEntered();
        Tag = tag;
    }
}

#pragma warning restore CS0649
#endif
