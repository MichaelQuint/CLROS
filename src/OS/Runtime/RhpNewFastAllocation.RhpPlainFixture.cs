using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649
#pragma warning restore CS0649
#pragma warning disable CS0649
#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE











#pragma warning disable 649
internal sealed class RhpPlainFixture
{
    internal ulong Value;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal RhpPlainFixture(ulong value)
    {
        RhpNewFastAllocation.ConstructorEntered();
        Value = value;
    }
}

#pragma warning restore 649
#endif
