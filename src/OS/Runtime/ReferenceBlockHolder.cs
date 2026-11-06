using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649 // Assigned by the enabled profile and mapped core tests.

#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE

#pragma warning disable CS0649

internal sealed class ReferenceBlockHolder
{
    internal ReferenceBlock Payload;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal ReferenceBlockHolder()
    {
        RhpNewFastAllocation.ConstructorEntered();
    }
}
#pragma warning restore CS0649
#endif
