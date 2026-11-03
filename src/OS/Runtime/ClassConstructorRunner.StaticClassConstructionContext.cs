#if NOSTDLIB_EFI && (M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE)
namespace System.Runtime.CompilerServices;

// Pinned ILC 11.0.0 RC2 win-x64 ABI: the compiler reserves exactly one pointer
// immediately before the non-GC static base. Runner state must live elsewhere.
public struct StaticClassConstructionContext
{
    public volatile nint cctorMethodAddress;
}

#endif
