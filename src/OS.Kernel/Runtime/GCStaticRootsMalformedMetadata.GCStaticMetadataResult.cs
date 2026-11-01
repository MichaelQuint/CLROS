using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
/// <summary>
/// An immediate read-only observation, never allocation, handle, root lifetime
/// or publication authority. Section payloads are not frozen by TypeManager.
/// Revalidate all original inputs rather than trusting a retained observation.
/// </summary>

public readonly struct GCStaticMetadataResult
{
    internal GCStaticMetadataResult(GCStaticMetadataStatus status, TypeManagerModuleStatus moduleStatus = TypeManagerModuleStatus.Success, ulong entry = 0, ulong cell = 0, ulong encodedCell = 0, ulong methodTable = 0, ulong relatedType = 0, ulong descriptor = 0, uint flags = 0, uint baseSize = 0, long adjustedSize = 0, ulong seriesOffset = 0, ulong seriesBytes = 0)
    {
        Status = status;
        ModuleStatus = moduleStatus;
        Entry = entry;
        Cell = cell;
        EncodedCell = encodedCell;
        MethodTable = methodTable;
        RelatedType = relatedType;
        Descriptor = descriptor;
        Flags = flags;
        BaseSize = baseSize;
        AdjustedSize = adjustedSize;
        SeriesOffset = seriesOffset;
        SeriesBytes = seriesBytes;
    }

    public GCStaticMetadataStatus Status { get; }
    public TypeManagerModuleStatus ModuleStatus { get; }
    public ulong Entry { get; }
    public ulong Cell { get; }
    public ulong EncodedCell { get; }
    public ulong MethodTable { get; }
    public ulong RelatedType { get; }
    public ulong Descriptor { get; }
    public uint Flags { get; }
    public uint BaseSize { get; }
    public long AdjustedSize { get; }
    public ulong SeriesOffset { get; }
    public ulong SeriesBytes { get; }
    public bool Succeeded => Status == GCStaticMetadataStatus.Success;
}
