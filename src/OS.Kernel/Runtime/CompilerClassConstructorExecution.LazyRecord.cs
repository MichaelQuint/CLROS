using OS.Boot;

namespace OS.Kernel.Runtime;
public static unsafe partial class CompilerClassConstructorExecution
{
    private struct LazyRecord
    {
        internal ulong Context, Target;
        internal uint Phase, Parent;
    }
}
