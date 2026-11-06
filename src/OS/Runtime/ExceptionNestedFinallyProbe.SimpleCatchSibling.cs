#if NOSTDLIB_EFI && M04_NESTED_FINALLY_CATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal sealed class SimpleCatchSibling : SimpleCatchBase
{
    internal SimpleCatchSibling(ulong payload) : base(payload)
    {
    }
}

#pragma warning disable CS0649

#pragma warning restore CS0649

#endif
