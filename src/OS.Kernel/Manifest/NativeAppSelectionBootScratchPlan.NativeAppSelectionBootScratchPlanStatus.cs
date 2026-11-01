using OS.Storage;

namespace OS.Manifest;
public enum NativeAppSelectionBootScratchPlanStatus
{
    Success = 0,
    InvalidSectorSize = 1,
    InvalidPartitionEntryCount = 2,
    InvalidPartitionEntrySize = 3,
    InvalidPartitionDescriptorCount = 4,
    InvalidManifestBufferRequirement = 5,
    InvalidPackageBufferRequirement = 6,
    PartitionEntrySectorBufferRequirementOverflow = 7,
    GptHeaderSectorBufferTooSmall = 8,
    GptPartitionEntrySectorBufferTooSmall = 9,
    GptPartitionDescriptorBufferTooSmall = 10,
    ManifestBufferTooSmall = 11,
    PackageBufferTooSmall = 12
}
