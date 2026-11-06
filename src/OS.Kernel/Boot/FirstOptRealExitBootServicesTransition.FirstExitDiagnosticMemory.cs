using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
// The adapter retains the original inputs in its image-resident state. All
// pointers must remain accessible/exclusive; declarations are not capabilities.
public unsafe struct FirstExitDiagnosticMemory
{
    public byte* Code;
    public EfiLoadedImageRangeCapturePlan Capture;
    public M01LoadedImageVirtualMappingEvidence Mapping;
    public FirstExitImageReferences References;
    public SeparatePageFaultMappingRange Allocator;
    public SeparatePageFaultPageTableRequest Tables;
    public X64PageTableImagePage* Pages;
    public ulong PageCapacity;
    public ulong* Entries;
    public ulong EntryCapacity;
}
