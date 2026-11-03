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
    private enum CompositeInstructionKind
    {
        Other,
        Jump,
        ConditionalBranch,
        DirectCall,
        IndirectCall,
        OtherIndirectControl,
        Ret,
        Cli,
        Sti,
        Pushfq,
        Popfq,
        Push,
        PushMemory,
        Pop,
        Cpuid,
        Rdmsr,
        Mfence,
        Invlpg,
        ReadCr0,
        ReadCr3,
        WriteCr3,
        ReadCr4,
        WriteCr4,
        OtherControlRegister,
        SubRsp,
        AddRsp,
        MoveRegisterImmediate,
        MoveRegisterRegister,
        AndRegisterRegister,
        AndRegisterImmediate,
        CompareRegisterRegister
    }
}
