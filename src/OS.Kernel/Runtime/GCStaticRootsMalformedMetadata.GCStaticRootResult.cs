using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
/// <summary>Scalar observation, never a CLR reference or a transferable write capability.</summary>

public readonly struct GCStaticRootResult
{
    internal GCStaticRootResult(GCStaticRootStatus status, GCStaticMetadataStatus metadataStatus = default, TypeManagerModuleStatus moduleStatus = default, RhpNewFastStatus heapStatus = default, ulong objectReference = 0, ulong cell = 0, ulong seriesStart = 0, ulong seriesBytes = 0, ulong allocationBytes = 0)
    {
        Status = status;
        MetadataStatus = metadataStatus;
        ModuleStatus = moduleStatus;
        HeapStatus = heapStatus;
        ObjectReference = objectReference;
        Cell = cell;
        SeriesStart = seriesStart;
        SeriesBytes = seriesBytes;
        AllocationBytes = allocationBytes;
    }

    public GCStaticRootStatus Status { get; }
    public GCStaticMetadataStatus MetadataStatus { get; }
    public TypeManagerModuleStatus ModuleStatus { get; }
    public RhpNewFastStatus HeapStatus { get; }
    public ulong ObjectReference { get; }
    public ulong Cell { get; }
    public ulong SeriesStart { get; }
    public ulong SeriesBytes { get; }
    public ulong AllocationBytes { get; }
    public bool Succeeded => Status == GCStaticRootStatus.Success;
}
