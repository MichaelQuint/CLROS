#if NOSTDLIB_EFI && (M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE || M04_NESTED_FINALLY_CATCH_PROBE || M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_FILTERED_CATCH_PROBE || M04_NORMAL_FINALLY_PROBE)
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;


#pragma warning disable CS0649

#pragma warning restore CS0649






#endif
#if SINGLE_FINALLY_CORE_TEST
using OS.Kernel.Runtime;
namespace OS.Runtime;
#endif
#if (NOSTDLIB_EFI && (M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE)) || SINGLE_FINALLY_CORE_TEST
// Pure admission predicates shared by the real EFI callbacks and an isolated
// host fixture compiling this same source. They do not authenticate a module,
// dereference guest addresses, search types or execute/restore a context.
internal static unsafe partial class SingleFinallyAdmission
{

    internal readonly struct Control(ulong flags, ulong cr4, uint mxcsr, ushort x87)
    {
        internal readonly ulong Flags = flags, Cr4 = cr4;
        internal readonly uint Mxcsr = mxcsr;
        internal readonly ushort X87 = x87;
    }

}
#endif
