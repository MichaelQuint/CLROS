using OS.Kernel.Runtime;

namespace OS.Runtime;
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#endif
// These offsets come from actual C# field-address roots in the enabled image.
// The bounded numeric checks below do not authenticate a compiler layout by themselves.
internal struct CheckedReferenceGeometry
{
#pragma warning disable CS0649 // Assigned by the enabled profile and mapped core tests.

    internal ulong CellReference, CellTag, CellPayload;
    internal ulong PairFirst, PairLast, PairTag, BlockPayload;
#pragma warning restore CS0649
}
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE

#pragma warning disable CS0649

#pragma warning restore CS0649
#endif
