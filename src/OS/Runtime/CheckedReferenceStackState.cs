using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649 // Assigned by the enabled profile and mapped core tests.

#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE && M04_CHECKED_REFERENCE_BARRIER_PROBE

#pragma warning disable CS0649

#pragma warning restore CS0649
#endif
// Separate from the unchanged 432-byte barrier context and never consumed by a
// returning helper. Production provenance comes only from the private M01 getter;
// mapped host callers supply readable storage for numeric admission controls.
internal struct CheckedReferenceStackState
{
    internal ulong Self, Authority, Start, End, InitialRsp, Witness;
    internal ulong Armed0, Armed1, Final0, Final1, Checks, Phase;
}
