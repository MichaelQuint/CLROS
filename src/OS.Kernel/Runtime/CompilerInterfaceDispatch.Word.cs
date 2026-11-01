using OS.Boot;

namespace OS.Kernel.Runtime;
public static unsafe partial class CompilerInterfaceDispatch
{
    private struct Word
    {
        internal ulong Address, Value, Bytes;
    }
}
