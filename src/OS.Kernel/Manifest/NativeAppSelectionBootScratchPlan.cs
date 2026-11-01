using OS.Storage;

namespace OS.Manifest;
public static class NativeAppSelectionBootScratchPlan
{
    public const uint DefaultSectorSizeBytes = 512;
    public const uint DefaultPartitionEntryCount = 128;
    public const uint DefaultPartitionEntrySizeBytes = GptPartitionTableParser.MinimumPartitionEntrySize;
    public const uint DefaultPartitionEntrySectorsRequired = 32;
    public const uint DefaultPartitionDescriptorCount = 1;
    public const uint DefaultManifestBufferBytes = 64 * 1024;
    public const uint DefaultPackageBufferBytes = 16 * 1024 * 1024;
    public const bool ReadsStorage = false;
    public const bool ParsesManifestOrPackage = false;
    public const bool AllocatesBuffers = false;
    public const bool ExecutesAppCode = false;
    public static NativeAppSelectionBootScratchPlanResult ValidateDefault(ReadOnlySpan<byte> primaryHeaderSector, ReadOnlySpan<byte> partitionEntrySectors, Span<GptPartitionDescriptor> partitions, ReadOnlySpan<byte> manifestBuffer, ReadOnlySpan<byte> packageBuffer)
    {
        return Validate(primaryHeaderSector, partitionEntrySectors, partitions, manifestBuffer, packageBuffer, DefaultSectorSizeBytes, DefaultPartitionEntryCount, DefaultPartitionEntrySizeBytes, DefaultPartitionDescriptorCount, DefaultManifestBufferBytes, DefaultPackageBufferBytes);
    }

    public static NativeAppSelectionBootScratchPlanResult Validate(ReadOnlySpan<byte> primaryHeaderSector, ReadOnlySpan<byte> partitionEntrySectors, Span<GptPartitionDescriptor> partitions, ReadOnlySpan<byte> manifestBuffer, ReadOnlySpan<byte> packageBuffer, uint sectorSizeBytes, uint partitionEntryCount, uint partitionEntrySizeBytes, uint partitionDescriptorCount, uint manifestBufferBytesRequired, uint packageBufferBytesRequired)
    {
        ulong headerAvailable = checked((ulong)primaryHeaderSector.Length);
        ulong partitionEntriesAvailable = checked((ulong)partitionEntrySectors.Length);
        uint partitionDescriptorsAvailable = checked((uint)partitions.Length);
        uint manifestAvailable = checked((uint)manifestBuffer.Length);
        uint packageAvailable = checked((uint)packageBuffer.Length);
        if (!IsSupportedSectorSize(sectorSizeBytes))
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.InvalidSectorSize, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, 0, 0, 0, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (partitionEntryCount == 0)
        {
            return CreateInvalidPartitionResult(NativeAppSelectionBootScratchPlanStatus.InvalidPartitionEntryCount, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, headerAvailable, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (partitionEntrySizeBytes < GptPartitionTableParser.MinimumPartitionEntrySize || (partitionEntrySizeBytes % 8) != 0)
        {
            return CreateInvalidPartitionResult(NativeAppSelectionBootScratchPlanStatus.InvalidPartitionEntrySize, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, headerAvailable, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (partitionDescriptorCount == 0)
        {
            return CreateInvalidPartitionResult(NativeAppSelectionBootScratchPlanStatus.InvalidPartitionDescriptorCount, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, headerAvailable, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (manifestBufferBytesRequired == 0)
        {
            return CreateInvalidPartitionResult(NativeAppSelectionBootScratchPlanStatus.InvalidManifestBufferRequirement, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, headerAvailable, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (packageBufferBytesRequired == 0)
        {
            return CreateInvalidPartitionResult(NativeAppSelectionBootScratchPlanStatus.InvalidPackageBufferRequirement, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, headerAvailable, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (!TryComputePartitionEntrySectorBytes(sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, out ulong partitionEntryBytesRequired, out ulong partitionEntrySectorsRequired, out ulong partitionEntrySectorBytesRequired))
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.PartitionEntrySectorBufferRequirementOverflow, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, 0, 0, 0, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (headerAvailable < sectorSizeBytes)
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.GptHeaderSectorBufferTooSmall, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, partitionEntryBytesRequired, partitionEntrySectorsRequired, partitionEntrySectorBytesRequired, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (partitionEntriesAvailable < partitionEntrySectorBytesRequired)
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.GptPartitionEntrySectorBufferTooSmall, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, partitionEntryBytesRequired, partitionEntrySectorsRequired, partitionEntrySectorBytesRequired, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (partitionDescriptorsAvailable < partitionDescriptorCount)
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.GptPartitionDescriptorBufferTooSmall, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, partitionEntryBytesRequired, partitionEntrySectorsRequired, partitionEntrySectorBytesRequired, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (manifestAvailable < manifestBufferBytesRequired)
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.ManifestBufferTooSmall, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, partitionEntryBytesRequired, partitionEntrySectorsRequired, partitionEntrySectorBytesRequired, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        if (packageAvailable < packageBufferBytesRequired)
        {
            return CreateResult(NativeAppSelectionBootScratchPlanStatus.PackageBufferTooSmall, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, partitionEntryBytesRequired, partitionEntrySectorsRequired, partitionEntrySectorBytesRequired, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
        }

        return CreateResult(NativeAppSelectionBootScratchPlanStatus.Success, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, partitionEntryBytesRequired, partitionEntrySectorsRequired, partitionEntrySectorBytesRequired, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
    }

    private static NativeAppSelectionBootScratchPlanResult CreateInvalidPartitionResult(NativeAppSelectionBootScratchPlanStatus status, uint sectorSizeBytes, uint partitionEntryCount, uint partitionEntrySizeBytes, uint partitionDescriptorCount, ulong headerAvailable, ulong partitionEntriesAvailable, uint partitionDescriptorsAvailable, uint manifestBufferBytesRequired, uint manifestAvailable, uint packageBufferBytesRequired, uint packageAvailable)
    {
        return CreateResult(status, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, sectorSizeBytes, headerAvailable, 0, 0, 0, partitionEntriesAvailable, partitionDescriptorsAvailable, manifestBufferBytesRequired, manifestAvailable, packageBufferBytesRequired, packageAvailable);
    }

    private static NativeAppSelectionBootScratchPlanResult CreateResult(NativeAppSelectionBootScratchPlanStatus status, uint sectorSizeBytes, uint partitionEntryCount, uint partitionEntrySizeBytes, uint partitionDescriptorCount, ulong gptHeaderSectorBytesRequired, ulong gptHeaderSectorBytesAvailable, ulong gptPartitionEntryBytesRequired, ulong gptPartitionEntrySectorsRequired, ulong gptPartitionEntrySectorBytesRequired, ulong gptPartitionEntrySectorBytesAvailable, uint gptPartitionDescriptorCountAvailable, uint manifestBytesRequired, uint manifestBytesAvailable, uint packageBytesRequired, uint packageBytesAvailable)
    {
        return NativeAppSelectionBootScratchPlanResult.Create(status, sectorSizeBytes, partitionEntryCount, partitionEntrySizeBytes, partitionDescriptorCount, gptHeaderSectorBytesRequired, gptHeaderSectorBytesAvailable, gptPartitionEntryBytesRequired, gptPartitionEntrySectorsRequired, gptPartitionEntrySectorBytesRequired, gptPartitionEntrySectorBytesAvailable, gptPartitionDescriptorCountAvailable, manifestBytesRequired, manifestBytesAvailable, packageBytesRequired, packageBytesAvailable);
    }

    private static bool TryComputePartitionEntrySectorBytes(uint sectorSizeBytes, uint partitionEntryCount, uint partitionEntrySizeBytes, out ulong partitionEntryBytesRequired, out ulong partitionEntrySectorsRequired, out ulong partitionEntrySectorBytesRequired)
    {
        partitionEntryBytesRequired = 0;
        partitionEntrySectorsRequired = 0;
        partitionEntrySectorBytesRequired = 0;
        ulong entryCount = partitionEntryCount;
        ulong entrySize = partitionEntrySizeBytes;
        if (entrySize != 0 && entryCount > ulong.MaxValue / entrySize)
        {
            return false;
        }

        partitionEntryBytesRequired = entryCount * entrySize;
        ulong sectorSize = sectorSizeBytes;
        if (partitionEntryBytesRequired > ulong.MaxValue - sectorSize + 1)
        {
            return false;
        }

        partitionEntrySectorsRequired = (partitionEntryBytesRequired + sectorSize - 1) / sectorSize;
        if (partitionEntrySectorsRequired == 0 || partitionEntrySectorsRequired > ulong.MaxValue / sectorSize)
        {
            return false;
        }

        partitionEntrySectorBytesRequired = partitionEntrySectorsRequired * sectorSize;
        return true;
    }

    private static bool IsSupportedSectorSize(uint sectorSizeBytes)
    {
        return sectorSizeBytes >= GptPartitionTableParser.MinimumSectorSizeBytes && sectorSizeBytes <= GptPartitionTableParser.MaximumSupportedSectorSizeBytes && (sectorSizeBytes % GptPartitionTableParser.MinimumSectorSizeBytes) == 0;
    }
}
