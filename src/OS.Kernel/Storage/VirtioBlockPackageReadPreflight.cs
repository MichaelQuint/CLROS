namespace OS.Storage;

public static class VirtioBlockPackageReadPreflight
{
    public const bool ValidatesCallerOwnedReadSession = true;
    public const bool PreservesReadSessionProvenance = true;
    public const bool RejectsIncompleteReadSessionBeforePackageParse = true;
    public const bool ValidatesGptHeaderScratch = true;
    public const bool ValidatesPartitionEntryScratch = true;
    public const bool ValidatesPackageBuffer = true;
    public const bool ReportsScratchCapacity = true;
    public const bool DelegatesPackageLoaderContracts = true;
    public const bool RequiresCallerOwnedBlockDevice = true;
    public const bool NotifiesLiveDevice = false;
    public const bool WaitsForHardware = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool ReadsRealDisk = false;
    public const bool WaitsForUsedRingCompletion = false;

    public const uint MinimumPackageBufferBytes = OsPackageParser.HeaderSize;

    public static VirtioBlockPackageReadPreflightResult LoadPackage(
        in VirtioBlockReadOnlyReadSessionResult session,
        IReadOnlyBlockDevice? disk,
        Span<byte> headerScratch,
        Span<byte> partitionEntryScratch,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> packageBuffer)
    {
        ulong headerScratchBytesAvailable = (ulong)headerScratch.Length;
        ulong partitionEntryScratchBytesAvailable = (ulong)partitionEntryScratch.Length;
        ulong packageBufferBytesAvailable = (ulong)packageBuffer.Length;

        if (!session.Succeeded)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.IncompleteReadSession,
                session,
                headerScratchBytesRequired: 0,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (disk is null ||
            !BlockDeviceInfo.Create(disk.Info.BlockSize, disk.Info.BlockCount).Succeeded)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.InvalidBlockDevice,
                session,
                headerScratchBytesRequired: 0,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        uint blockSize = disk.Info.BlockSize;
        ulong headerScratchBytesRequired = blockSize;

        if (headerScratchBytesAvailable < headerScratchBytesRequired)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.HeaderScratchTooSmall,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (partitionEntryScratchBytesAvailable < blockSize)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.PartitionEntryScratchTooSmall,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (partitions.Length < 1)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.PartitionDescriptorTableEmpty,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (packageBufferBytesAvailable < MinimumPackageBufferBytes)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.PackageBufferTooSmall,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        OsPackageLoadResult packageLoad = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            disk,
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return VirtioBlockPackageReadPreflightResult.FromPackageLoad(
            session,
            headerScratchBytesRequired,
            headerScratchBytesAvailable,
            partitionEntryScratchBytesAvailable,
            partitions.Length,
            packageBufferBytesRequired: MinimumPackageBufferBytes,
            packageBufferBytesAvailable,
            packageLoad);
    }
}
