#if NOSTDLIB_EFI && M04_INTERFACE_DISPATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal interface IDispatchProducer<out T>
{
    ulong Produce(ulong a, ulong b, ulong c, ulong d);
}

#endif
