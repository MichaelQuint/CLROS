using OS.Boot;

namespace OS.Kernel.Runtime;
public struct X64FrameUnwindResult
{
    public X64FrameUnwindStatus Status;
    public X64UnwindContext Caller;
    public ulong ImageBase, ModuleOwner, ModuleHandle, EstablisherFrame, ReturnSlot;
    public uint FunctionIndex;
    public bool InEpilog;
    public readonly bool Succeeded => Status == X64FrameUnwindStatus.Success;
}
