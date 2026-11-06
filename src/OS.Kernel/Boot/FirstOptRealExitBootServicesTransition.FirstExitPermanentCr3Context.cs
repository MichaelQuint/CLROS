using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
// Fixed x64 layout consumed by FirstExitPermanentCr3Stub. Retain the original
// context exclusively; do not change inputs after preparation or reset Phase.
public unsafe struct FirstExitPermanentCr3Context
{
    public ulong FirmwareCr3, OwnedCr3, FirmwareCr4;
    public ulong StackBottom, StackTop, MinimumRsp;
    public ulong LowerCanary, UpperCanary;
    public delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> Diagnostic;
    public void* DiagnosticContext;
    public ulong ObservedCr3, ObservedCr4;
    public FirstExitAdoptionPhase Phase;
    internal uint Reserved;
    public ulong ObservedRsp;
    public FirstExitDescriptorContext* Descriptors;
}
