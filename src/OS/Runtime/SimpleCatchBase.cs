#if NOSTDLIB_EFI && (M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE || M04_NESTED_FINALLY_CATCH_PROBE || M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_FILTERED_CATCH_PROBE || M04_NORMAL_FINALLY_PROBE)
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal class SimpleCatchBase : System.Exception
{
    internal readonly ulong Payload;
    internal SimpleCatchBase(ulong payload)
    {
        Payload = payload;
    }
}


#pragma warning disable CS0649

#pragma warning restore CS0649






#endif
#if SINGLE_FINALLY_CORE_TEST
using OS.Kernel.Runtime;
namespace OS.Runtime;
#endif
