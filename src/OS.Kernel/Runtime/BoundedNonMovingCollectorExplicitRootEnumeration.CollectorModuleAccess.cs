using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>Original mapped image inputs, not copied module authority.</summary>

public unsafe struct CollectorModuleAccess
{
    public EfiLoadedImageRangeCapturePlan Capture;
    public M01LoadedImageVirtualMappingEvidence Mapping;
    public byte* Owner;
    public ulong Capacity;
    public ulong Handle;
    public GCStaticReadableWindow Metadata;
}
