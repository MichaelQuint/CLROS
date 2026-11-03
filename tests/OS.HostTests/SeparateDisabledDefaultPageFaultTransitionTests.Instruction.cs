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
    private readonly record struct Instruction(string Op, int Width, Operand Destination, Operand Source, ulong Immediate, ulong Next, byte Condition = 0);
}
