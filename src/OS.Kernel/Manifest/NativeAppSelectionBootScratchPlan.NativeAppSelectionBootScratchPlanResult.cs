using OS.Storage;

namespace OS.Manifest;

public readonly struct NativeAppSelectionBootScratchPlanResult
{
    private NativeAppSelectionBootScratchPlanResult(NativeAppSelectionBootScratchPlanStatus status, bool isInitialized, uint sectorSizeBytes, uint partitionEntryCount, uint partitionEntrySizeBytes, uint partitionDescriptorCountRequired, ulong gptHeaderSectorBytesRequired, ulong gptHeaderSectorBytesAvailable, ulong gptPartitionEntryBytesRequired, ulong gptPartitionEntrySectorsRequired, ulong gptPartitionEntrySectorBytesRequired, ulong gptPartitionEntrySectorBytesAvailable, uint gptPartitionDescriptorCountAvailable, uint manifestBytesRequired, uint manifestBytesAvailable, uint packageBytesRequired, uint packageBytesAvailable)
    {
        Status = status;
        IsInitialized = isInitialized;
        SectorSizeBytes = sectorSizeBytes;
        PartitionEntryCount = partitionEntryCount;
        PartitionEntrySizeBytes = partitionEntrySizeBytes;
        PartitionDescriptorCountRequired = partitionDescriptorCountRequired;
        GptHeaderSectorBytesRequired = gptHeaderSectorBytesRequired;
        GptHeaderSectorBytesAvailable = gptHeaderSectorBytesAvailable;
        GptPartitionEntryBytesRequired = gptPartitionEntryBytesRequired;
        GptPartitionEntrySectorsRequired = gptPartitionEntrySectorsRequired;
        GptPartitionEntrySectorBytesRequired = gptPartitionEntrySectorBytesRequired;
        GptPartitionEntrySectorBytesAvailable = gptPartitionEntrySectorBytesAvailable;
        GptPartitionDescriptorCountAvailable = gptPartitionDescriptorCountAvailable;
        ManifestBytesRequired = manifestBytesRequired;
        ManifestBytesAvailable = manifestBytesAvailable;
        PackageBytesRequired = packageBytesRequired;
        PackageBytesAvailable = packageBytesAvailable;
    }

    public NativeAppSelectionBootScratchPlanStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public uint SectorSizeBytes
    {
        get;
    }
    public uint PartitionEntryCount
    {
        get;
    }
    public uint PartitionEntrySizeBytes
    {
        get;
    }
    public uint PartitionDescriptorCountRequired
    {
        get;
    }
    public ulong GptHeaderSectorBytesRequired
    {
        get;
    }
    public ulong GptHeaderSectorBytesAvailable
    {
        get;
    }
    public ulong GptPartitionEntryBytesRequired
    {
        get;
    }
    public ulong GptPartitionEntrySectorsRequired
    {
        get;
    }
    public ulong GptPartitionEntrySectorBytesRequired
    {
        get;
    }
    public ulong GptPartitionEntrySectorBytesAvailable
    {
        get;
    }
    public uint GptPartitionDescriptorCountAvailable
    {
        get;
    }
    public uint ManifestBytesRequired
    {
        get;
    }
    public uint ManifestBytesAvailable
    {
        get;
    }
    public uint PackageBytesRequired
    {
        get;
    }
    public uint PackageBytesAvailable
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppSelectionBootScratchPlanStatus.Success;

    internal static NativeAppSelectionBootScratchPlanResult Create(NativeAppSelectionBootScratchPlanStatus status, uint sectorSizeBytes, uint partitionEntryCount, uint partitionEntrySizeBytes, uint partitionDescriptorCountRequired, ulong gptHeaderSectorBytesRequired, ulong gptHeaderSectorBytesAvailable, ulong gptPartitionEntryBytesRequired, ulong gptPartitionEntrySectorsRequired, ulong gptPartitionEntrySectorBytesRequired, ulong gptPartitionEntrySectorBytesAvailable, uint gptPartitionDescriptorCountAvailable, uint manifestBytesRequired, uint manifestBytesAvailable, uint packageBytesRequired, uint packageBytesAvailable)
    {
        return new NativeAppSelectionBootScratchPlanResult(status, isInitialized: true, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCountRequired, gptHeaderSectorBytesRequired, gptHeaderSectorBytesAvailable, gptPartitionEntryBytesRequired, gptPartitionEntrySectorsRequired, gptPartitionEntrySectorBytesRequired, gptPartitionEntrySectorBytesAvailable, gptPartitionDescriptorCountAvailable, manifestBytesRequired, manifestBytesAvailable, packageBytesRequired, packageBytesAvailable);
    }
}
