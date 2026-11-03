#if NOSTDLIB_EFI && M04_INTERFACE_DISPATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal class DispatchReceiverB : DispatchOwnerBase, IDispatchConsumer<DispatchArgumentDerived>,
    IDispatchProducer<DispatchArgumentBase>, IDispatchNoise
{
    internal DispatchReceiverB(ulong value) : base(value) { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override ulong Virtual(ulong a, ulong b, ulong c, ulong d) => seed + 300 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Produce(ulong a, ulong b, ulong c, ulong d) => seed + 500 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Consume(ulong a, ulong b, ulong c, ulong d) => seed + 700 + a + 3 * b + 5 * c + 7 * d;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ulong Noise(ulong a, ulong b, ulong c, ulong d) => seed + 900 + a + 3 * b + 5 * c + 7 * d;
}

#endif
