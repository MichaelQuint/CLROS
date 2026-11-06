using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class CompilerInterfaceDispatch
{
    private struct Cache
    {
        internal ulong Receiver, Cell;
        internal CompilerInterfaceDispatchResolution Resolution;
    }
}
