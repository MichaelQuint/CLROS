using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Emitter = OS.Kernel.Diagnostics.DoubleFaultISTPathSilentRebootOrTriple;

internal static unsafe partial class DoubleFaultISTPathSilentRebootOrTripleTests
{
    // Restricted independent x64 decoder/interpreter. This interprets data using
    // ordinary managed operations; it never creates or invokes an executable pointer.
    private readonly record struct Operand(int Register, int Base = -1, int Index = -1, int Scale = 1, long Displacement = 0, bool Rip = false)
    {
        public static Operand Reg(int register) => new(register);
    }
}
