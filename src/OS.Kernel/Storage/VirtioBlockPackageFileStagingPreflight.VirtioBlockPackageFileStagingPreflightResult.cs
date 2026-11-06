namespace OS.Storage;

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

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockPackageFileStagingPreflightStatus Status
    {
        get;
    }

    public VirtioBlockPackageReadPreflightResult PackagePreflight
    {
        get;
    }

    public OsPackageOpenFileResult PackageOpen
    {
        get;
    }

    public OsPackageFileStagingResult PackageStaging
    {
        get;
    }

    public ulong StagingDestinationBytesAvailable
    {
        get;
    }

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
