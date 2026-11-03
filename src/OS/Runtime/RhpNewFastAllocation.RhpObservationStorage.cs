using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649
#pragma warning restore CS0649
internal unsafe struct RhpObservationStorage
{
    internal ulong CanaryBefore;
    internal RhpObjectObservation Object0;
    // Allocate writes these sequential POD slots through (&records->Object0)[id - 1].
#pragma warning disable CS0649
    internal RhpObjectObservation Object1, Object2, Object3, Object4;
    internal RhpObjectObservation Object5, Object6, Object7, Object8, Object9;
#pragma warning restore CS0649
    internal fixed byte BeforeAttempt[4096];
    internal ulong CanaryAfter;
}
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE











#pragma warning disable 649

#pragma warning restore 649
#endif
