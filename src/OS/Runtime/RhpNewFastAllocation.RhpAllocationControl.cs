using OS.Kernel.Runtime;

namespace OS.Runtime;

internal struct RhpAllocationControl
{
    internal ulong CanaryBefore, Self, Access, Bump, Records, Image, ImageBytes;
    internal ulong Prepared, InProgress, Completed;
    // Whole-POD initialization leaves this zero; only the enabled EFI profile increments it.
#pragma warning disable CS0649
    internal ulong ConstructorCount;
#pragma warning restore CS0649
    internal ulong CanaryAfter;
}
#pragma warning disable CS0649
#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE

#pragma warning disable 649

#pragma warning restore 649
#endif
