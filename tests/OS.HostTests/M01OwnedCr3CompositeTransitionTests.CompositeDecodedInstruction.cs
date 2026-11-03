using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class M01OwnedCr3CompositeTransitionTests
{
    private readonly record struct CompositeDecodedInstruction(int Offset, int Length, CompositeInstructionKind Kind, int? BranchTarget, int Register, ulong RegisterMask, uint? StateStoreOffset, uint? StateLoadOffset, int SourceRegister, int DestinationRegister, ulong Immediate, bool IsShiftLeftRdx32, bool IsOrRaxRdx, bool IsCr4PaeTest, byte Opcode, byte ModRm, int MemoryBaseRegister, uint? MemoryDisplacement);
}
