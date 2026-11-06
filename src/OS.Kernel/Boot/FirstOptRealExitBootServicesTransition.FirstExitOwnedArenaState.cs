using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;

public unsafe struct FirstExitOwnedArenaState
{
    internal byte* Data;
    internal uint Phase;
    internal uint Reserved;
    public bool IsReady => Phase == 2;
    public bool FailedAfterWrite => Phase == 3;
    public byte* Storage => Data;
    public FirstExitDescriptorContext* Descriptors => IsReady ? (FirstExitDescriptorContext*)Data : null;
    public FirstExitOwnedStackContext* StackEntry => IsReady ? (FirstExitOwnedStackContext*)(Data + 128) : null;
    public FirstExitPermanentCr3Context* Permanent => IsReady ? (FirstExitPermanentCr3Context*)(Data + 256) : null;
    public FirstExitFirmwareBindingState* Binding => IsReady ? (FirstExitFirmwareBindingState*)(Data + 384) : null;
    public FirstExitTransitionState* Transition => IsReady ? (FirstExitTransitionState*)(Data + 512) : null;
    public FirstExitFaultContext* Fault => IsReady ? (FirstExitFaultContext*)(Data + 1024) : null;
    public byte* Map => IsReady ? Data + 5 * 4096 : null;
    public ulong StackBottom => IsReady ? (ulong)Data + 22 * 4096 + 8 : 0;
    public ulong StackTop => IsReady ? (ulong)Data + 86 * 4096 - 16 : 0;
}
