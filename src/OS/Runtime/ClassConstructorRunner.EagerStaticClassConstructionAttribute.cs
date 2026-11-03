#if NOSTDLIB_EFI && (M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE)
namespace System.Runtime.CompilerServices;

internal sealed class EagerStaticClassConstructionAttribute : Attribute
{
}

#endif
