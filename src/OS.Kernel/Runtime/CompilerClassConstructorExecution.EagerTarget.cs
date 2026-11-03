using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class CompilerClassConstructorExecution
{
    // Eager identity is the table entry/order, not its callable address: ILC or
    // the linker may fold distinct constructor bodies to the same target.
    private struct EagerTarget
    {
        internal ulong Entry, Target;
    }
}
