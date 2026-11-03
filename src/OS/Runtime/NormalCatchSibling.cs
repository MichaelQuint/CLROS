#if NOSTDLIB_EFI && M04_NORMAL_FINALLY_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal sealed class NormalCatchSibling : SimpleCatchBase
{
    internal NormalCatchSibling(ulong payload) : base(payload) { }
}

#endif
