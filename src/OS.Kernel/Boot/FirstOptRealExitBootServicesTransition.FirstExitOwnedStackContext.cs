using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
// Native entry ABI. Keep one original context, exclusively owned and immutable
// after preparation. Entry receives this pointer and reads its EntryContext.
public unsafe struct FirstExitOwnedStackContext
{
    public ulong FirmwareCr3, FirmwareCr4, StackBottom, StackTop;
    public ulong LowerCanary, UpperCanary;
    public delegate* unmanaged<FirstExitOwnedStackContext*, void> Entry;
    public void* EntryContext;
    public ulong EntryStackBytes, ObservedRsp;
    public uint Phase;
    internal uint Reserved;
}
