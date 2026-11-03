#if NOSTDLIB_EFI && M04_INTERFACE_DISPATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// Base owns the nonvirtual interface implementation. The two derived receiver
// layouts retain one scalar field and deliberately order other interfaces
// differently. Calling Noise ensures that interface survives compiler trimming.
internal class DispatchOwnerBase : IDispatchPlain, IDispatchVirtual
{
    protected readonly ulong seed;
    internal DispatchOwnerBase(ulong value)
    {
        RhpNewFastAllocation.DispatchConstructed(value);
        seed = value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Sum(ulong a, ulong b, ulong c, ulong d) => seed + 100 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual ulong Virtual(ulong a, ulong b, ulong c, ulong d) => seed + a + b + c + d;
}

#endif
