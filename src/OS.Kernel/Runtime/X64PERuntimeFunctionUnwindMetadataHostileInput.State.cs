using OS.Boot;

namespace OS.Kernel.Runtime;
public static unsafe partial class X64PERuntimeFunctionUnwindMetadataHostileInput
{
    private struct State
    {
        internal ulong Phase, Self, Capacity, Required;
        internal Image Image;
    }
}
