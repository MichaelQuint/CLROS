using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Emitter = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition;

internal static unsafe partial class SeparateDisabledDefaultPageFaultTransitionTests
{
    // Restricted independent x64 decoder/interpreter. This interprets data using
    // ordinary managed operations; it never creates or invokes an executable pointer.
    private readonly record struct Operand(int Register, int Base = -1, int Index = -1, int Scale = 1, long Displacement = 0, bool Rip = false)
    {
        public static Operand Reg(int register) => new(register);
    }
}
