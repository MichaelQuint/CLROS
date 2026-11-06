using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>Metadata observations only; no handler, stack slot or language-specific data is executed/read.</summary>

public struct X64UnwindMetadataResult
{
    public X64UnwindMetadataStatus Status;
    public ulong RequiredStorage, ImageBase, ModuleOwner, ModuleHandle;
    public uint FunctionCount, FunctionIndex, BeginRva, EndRva, UnwindRva, PrimaryUnwindRva;
    public uint HandlerRva, LanguageDataStartRva, FixedAllocationBytes, StackBytes, ChainDepth;
    public byte PrologBytes, CodeSlots, FrameRegister, FrameOffset, Flags, PrimaryFlags;
    public bool Succeeded => Status == X64UnwindMetadataStatus.Success;
}
