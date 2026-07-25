namespace OS.Storage;

public enum VirtioBlockPackageFileStagingPreflightStatus
{
    Success = 0,
    PackagePreflightFailed = 1,
    PackageOpenFailed = 2,
    PackageStagingFailed = 3
}

public readonly struct VirtioBlockPackageFileStagingPreflightResult
{
    private VirtioBlockPackageFileStagingPreflightResult(
        VirtioBlockPackageFileStagingPreflightStatus status,
        VirtioBlockPackageReadPreflightResult packagePreflight,
        OsPackageOpenFileResult packageOpen,
        OsPackageFileStagingResult packageStaging,
        ulong stagingDestinationBytesAvailable)
    {
        IsInitialized = true;
        Status = status;
        PackagePreflight = packagePreflight;
        PackageOpen = packageOpen;
        PackageStaging = packageStaging;
        StagingDestinationBytesAvailable = stagingDestinationBytesAvailable;
    }

    public bool IsInitialized { get; }

    public VirtioBlockPackageFileStagingPreflightStatus Status { get; }

    public VirtioBlockPackageReadPreflightResult PackagePreflight { get; }

    public OsPackageOpenFileResult PackageOpen { get; }

    public OsPackageFileStagingResult PackageStaging { get; }

    public ulong StagingDestinationBytesAvailable { get; }

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus =>
        PackagePreflight.ReadSessionStatus;

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus =>
        PackagePreflight.ReadSessionPipelineStatus;

    public VirtioBlockPackageReadPreflightStatus PackagePreflightStatus =>
        PackagePreflight.Status;

    public OsPackageFileSystemStatus PackageOpenStatus => PackageOpen.Status;

    public OsPackageFileStagingStatus PackageStagingStatus => PackageStaging.Status;

    public ulong ReadSessionByteCount => PackagePreflight.ReadSessionByteCount;

    public uint PackageBytesRead => PackagePreflight.PackageLoad.BytesRead;

    public ulong PackageFileBytesStaged => PackageStaging.BytesWritten;

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockPackageFileStagingPreflightStatus.Success &&
        PackagePreflight.Succeeded &&
        PackageOpen.Succeeded &&
        PackageStaging.Succeeded;

    internal static VirtioBlockPackageFileStagingPreflightResult FromPackagePreflight(
        VirtioBlockPackageReadPreflightResult packagePreflight,
        ulong stagingDestinationBytesAvailable)
    {
        return new VirtioBlockPackageFileStagingPreflightResult(
            VirtioBlockPackageFileStagingPreflightStatus.PackagePreflightFailed,
            packagePreflight,
            default,
            default,
            stagingDestinationBytesAvailable);
    }

    internal static VirtioBlockPackageFileStagingPreflightResult FromPackageOpen(
        VirtioBlockPackageReadPreflightResult packagePreflight,
        OsPackageOpenFileResult packageOpen,
        ulong stagingDestinationBytesAvailable)
    {
        return new VirtioBlockPackageFileStagingPreflightResult(
            VirtioBlockPackageFileStagingPreflightStatus.PackageOpenFailed,
            packagePreflight,
            packageOpen,
            default,
            stagingDestinationBytesAvailable);
    }

    internal static VirtioBlockPackageFileStagingPreflightResult FromPackageStaging(
        VirtioBlockPackageReadPreflightResult packagePreflight,
        OsPackageOpenFileResult packageOpen,
        OsPackageFileStagingResult packageStaging,
        ulong stagingDestinationBytesAvailable)
    {
        VirtioBlockPackageFileStagingPreflightStatus status = packageStaging.Succeeded
            ? VirtioBlockPackageFileStagingPreflightStatus.Success
            : VirtioBlockPackageFileStagingPreflightStatus.PackageStagingFailed;

        return new VirtioBlockPackageFileStagingPreflightResult(
            status,
            packagePreflight,
            packageOpen,
            packageStaging,
            stagingDestinationBytesAvailable);
    }
}

public static class VirtioBlockPackageFileStagingPreflight
{
    public const bool ValidatesCallerOwnedReadSession = true;
    public const bool DelegatesPackageReadPreflight = true;
    public const bool OpensPackageFileBeforeStaging = true;
    public const bool DelegatesPackageFileStagingPlanner = true;
    public const bool WritesCallerOwnedDestinationOnly = true;
    public const bool NotifiesLiveDevice = false;
    public const bool WaitsForHardware = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool ReadsRealDisk = false;
    public const bool AllocatesStagingMemory = false;
    public const bool MapsExecutablePages = false;
    public const bool ExecutesAppCode = false;
    public const bool StartsHostedDotNet = false;
    public const bool StartsKestrel = false;

    public static VirtioBlockPackageFileStagingPreflightResult StagePackageFile(
        in VirtioBlockReadOnlyReadSessionResult session,
        IReadOnlyBlockDevice? disk,
        Span<byte> headerScratch,
        Span<byte> partitionEntryScratch,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> packageBuffer,
        ReadOnlySpan<byte> packagePath,
        Span<byte> stagingDestination)
    {
        ulong stagingDestinationBytesAvailable = (ulong)stagingDestination.Length;
        VirtioBlockPackageReadPreflightResult packagePreflight =
            VirtioBlockPackageReadPreflight.LoadPackage(
                session,
                disk,
                headerScratch,
                partitionEntryScratch,
                partitions,
                packageBuffer);
        if (!packagePreflight.Succeeded)
        {
            return VirtioBlockPackageFileStagingPreflightResult.FromPackagePreflight(
                packagePreflight,
                stagingDestinationBytesAvailable);
        }

        ReadOnlySpan<byte> packageBytes = packageBuffer.Slice(
            0,
            checked((int)packagePreflight.PackageLoad.BytesRead));
        OsPackageOpenFileResult packageOpen =
            OsPackageFileSystem.OpenFile(packageBytes, packagePath);
        if (!packageOpen.Succeeded)
        {
            return VirtioBlockPackageFileStagingPreflightResult.FromPackageOpen(
                packagePreflight,
                packageOpen,
                stagingDestinationBytesAvailable);
        }

        OsPackageFileStagingResult packageStaging =
            OsPackageFileStagingPlanner.StageOpenedFile(
                packageBytes,
                packageOpen,
                stagingDestination);

        return VirtioBlockPackageFileStagingPreflightResult.FromPackageStaging(
            packagePreflight,
            packageOpen,
            packageStaging,
            stagingDestinationBytesAvailable);
    }
}
