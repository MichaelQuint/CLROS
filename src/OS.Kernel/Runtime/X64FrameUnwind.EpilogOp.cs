using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class X64FrameUnwind
{
    private struct EpilogOp
    {
        internal byte Kind, Register;
        internal long Amount;
    }
}
