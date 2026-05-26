using OS.Kernel.Runtime;
using OS.Storage;

namespace OS.Manifest;

public enum NativeAppEntryStagingPreflightStatus
{
    Success = 0,
    NotInitialized = 1,
    ManifestLoadFailed = 2,
    PackageParseFailed = 3,
    LaunchPlanFailed = 4,
    LaunchDescriptorFailed = 5,
    PackageFileStagingFailed = 6,
    PayloadMappingFailed = 7,
    PayloadStagingFailed = 8,
    ExecutableMemoryPlanFailed = 9,
    InvocationPlanFailed = 10,
    MissingPackageStagingBytes = 11,
    MissingPayloadStagingBytes = 12
}

public readonly struct NativeAppEntryStagingPreflightResult
{
    private NativeAppEntryStagingPreflightResult(
        NativeAppEntryStagingPreflightStatus status,
        NativeAppPackageStagingReadinessStatus stagingReadinessStatus,
        NativeManifestLoadStatus manifestStatus,
        OsPackageStatus packageStatus,
        NativeAppLaunchStatus launchStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        OsPackageFileStagingStatus packageFileStagingStatus,
        NativeAppPayloadMappingPlanStatus payloadMappingStatus,
        NativeAppPayloadStagingStatus payloadStagingStatus,
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppInvocationPlanStatus invocationStatus,
        bool isInitialized,
        uint appBinaryFileIndex,
        ulong packageFileBytesStaged,
        ulong payloadExecutableBytesStaged,
        ulong requiredWritableStagingBytes,
        ulong requiredStackBytes,
        ulong entryOffsetInPackage)
    {
        Status = status;
        StagingReadinessStatus = stagingReadinessStatus;
        ManifestStatus = manifestStatus;
        PackageStatus = packageStatus;
        LaunchStatus = launchStatus;
        DescriptorStatus = descriptorStatus;
        PackageFileStagingStatus = packageFileStagingStatus;
        PayloadMappingStatus = payloadMappingStatus;
        PayloadStagingStatus = payloadStagingStatus;
        ExecutableMemoryStatus = executableMemoryStatus;
        InvocationStatus = invocationStatus;
        IsInitialized = isInitialized;
        AppBinaryFileIndex = appBinaryFileIndex;
        PackageFileBytesStaged = packageFileBytesStaged;
        PayloadExecutableBytesStaged = payloadExecutableBytesStaged;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        RequiredStackBytes = requiredStackBytes;
        EntryOffsetInPackage = entryOffsetInPackage;
    }

    public NativeAppEntryStagingPreflightStatus Status { get; }

    public NativeAppPackageStagingReadinessStatus StagingReadinessStatus { get; }

    public NativeManifestLoadStatus ManifestStatus { get; }

    public OsPackageStatus PackageStatus { get; }

    public NativeAppLaunchStatus LaunchStatus { get; }

    public NativeAppLaunchDescriptorStatus DescriptorStatus { get; }

    public OsPackageFileStagingStatus PackageFileStagingStatus { get; }

    public NativeAppPayloadMappingPlanStatus PayloadMappingStatus { get; }

    public NativeAppPayloadStagingStatus PayloadStagingStatus { get; }

    public NativeAppExecutableMemoryPlanStatus ExecutableMemoryStatus { get; }

    public NativeAppInvocationPlanStatus InvocationStatus { get; }

    public bool IsInitialized { get; }

    public uint AppBinaryFileIndex { get; }

    public ulong PackageFileBytesStaged { get; }

    public ulong PayloadExecutableBytesStaged { get; }

    public ulong RequiredWritableStagingBytes { get; }

    public ulong RequiredStackBytes { get; }

    public ulong EntryOffsetInPackage { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == NativeAppEntryStagingPreflightStatus.Success &&
        StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success &&
        PackageFileBytesStaged != 0 &&
        PayloadExecutableBytesStaged != 0 &&
        RequiredWritableStagingBytes != 0 &&
        RequiredStackBytes != 0;

    public static NativeAppEntryStagingPreflightResult Success(
        NativeAppPackageStagingReadinessStatus stagingReadinessStatus,
        NativeManifestLoadStatus manifestStatus,
        OsPackageStatus packageStatus,
        NativeAppLaunchStatus launchStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        OsPackageFileStagingStatus packageFileStagingStatus,
        NativeAppPayloadMappingPlanStatus payloadMappingStatus,
        NativeAppPayloadStagingStatus payloadStagingStatus,
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppInvocationPlanStatus invocationStatus,
        uint appBinaryFileIndex,
        ulong packageFileBytesStaged,
        ulong payloadExecutableBytesStaged,
        ulong requiredWritableStagingBytes,
        ulong requiredStackBytes,
        ulong entryOffsetInPackage)
    {
        return new NativeAppEntryStagingPreflightResult(
            NativeAppEntryStagingPreflightStatus.Success,
            stagingReadinessStatus,
            manifestStatus,
            packageStatus,
            launchStatus,
            descriptorStatus,
            packageFileStagingStatus,
            payloadMappingStatus,
            payloadStagingStatus,
            executableMemoryStatus,
            invocationStatus,
            isInitialized: true,
            appBinaryFileIndex,
            packageFileBytesStaged,
            payloadExecutableBytesStaged,
            requiredWritableStagingBytes,
            requiredStackBytes,
            entryOffsetInPackage);
    }

    public static NativeAppEntryStagingPreflightResult Fail(
        NativeAppEntryStagingPreflightStatus status,
        NativeAppPackageStagingReadinessStatus stagingReadinessStatus,
        NativeManifestLoadStatus manifestStatus,
        OsPackageStatus packageStatus,
        NativeAppLaunchStatus launchStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        OsPackageFileStagingStatus packageFileStagingStatus,
        NativeAppPayloadMappingPlanStatus payloadMappingStatus,
        NativeAppPayloadStagingStatus payloadStagingStatus,
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppInvocationPlanStatus invocationStatus,
        uint appBinaryFileIndex,
        ulong packageFileBytesStaged,
        ulong payloadExecutableBytesStaged,
        ulong requiredWritableStagingBytes,
        ulong requiredStackBytes,
        ulong entryOffsetInPackage)
    {
        return new NativeAppEntryStagingPreflightResult(
            status,
            stagingReadinessStatus,
            manifestStatus,
            packageStatus,
            launchStatus,
            descriptorStatus,
            packageFileStagingStatus,
            payloadMappingStatus,
            payloadStagingStatus,
            executableMemoryStatus,
            invocationStatus,
            isInitialized: true,
            appBinaryFileIndex,
            packageFileBytesStaged,
            payloadExecutableBytesStaged,
            requiredWritableStagingBytes,
            requiredStackBytes,
            entryOffsetInPackage);
    }
}

public static class NativeAppEntryStagingPreflight
{
    public const bool RequiresPackageStagingReadiness = true;
    public const bool RequiresCallerOwnedPackageFileStaging = true;
    public const bool RequiresCallerOwnedPayloadStaging = true;
    public const bool ReadsLiveStorage = false;
    public const bool AllocatesMemory = false;
    public const bool MapsExecutablePages = false;
    public const bool ChangesPagePermissions = false;
    public const bool ExecutesAppCode = false;

    public static NativeAppEntryStagingPreflightResult FromPackageStagingReadiness(
        NativeAppPackageStagingReadinessResult readiness)
    {
        NativeAppEntryStagingPreflightStatus status = GetStatus(readiness);
        return status == NativeAppEntryStagingPreflightStatus.Success
            ? NativeAppEntryStagingPreflightResult.Success(
                readiness.Status,
                readiness.ManifestStatus,
                readiness.PackageStatus,
                readiness.LaunchStatus,
                readiness.DescriptorStatus,
                readiness.PackageFileStagingStatus,
                readiness.PayloadMappingStatus,
                readiness.PayloadStagingStatus,
                readiness.ExecutableMemoryStatus,
                readiness.InvocationStatus,
                readiness.AppBinaryFileIndex,
                readiness.PackageFileBytesStaged,
                readiness.PayloadExecutableBytesStaged,
                readiness.RequiredWritableStagingBytes,
                readiness.RequiredStackBytes,
                readiness.EntryOffsetInPackage)
            : NativeAppEntryStagingPreflightResult.Fail(
                status,
                readiness.Status,
                readiness.ManifestStatus,
                readiness.PackageStatus,
                readiness.LaunchStatus,
                readiness.DescriptorStatus,
                readiness.PackageFileStagingStatus,
                readiness.PayloadMappingStatus,
                readiness.PayloadStagingStatus,
                readiness.ExecutableMemoryStatus,
                readiness.InvocationStatus,
                readiness.AppBinaryFileIndex,
                readiness.PackageFileBytesStaged,
                readiness.PayloadExecutableBytesStaged,
                readiness.RequiredWritableStagingBytes,
                readiness.RequiredStackBytes,
                readiness.EntryOffsetInPackage);
    }

    private static NativeAppEntryStagingPreflightStatus GetStatus(
        NativeAppPackageStagingReadinessResult readiness)
    {
        if (!readiness.IsInitialized)
        {
            return NativeAppEntryStagingPreflightStatus.NotInitialized;
        }

        NativeAppEntryStagingPreflightStatus dependencyStatus =
            MapDependencyStatus(readiness.Status);
        if (dependencyStatus != NativeAppEntryStagingPreflightStatus.Success)
        {
            return dependencyStatus;
        }

        if (!readiness.Succeeded)
        {
            return NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed;
        }

        if (readiness.PackageFileBytesStaged == 0)
        {
            return NativeAppEntryStagingPreflightStatus.MissingPackageStagingBytes;
        }

        if (readiness.PayloadExecutableBytesStaged == 0)
        {
            return NativeAppEntryStagingPreflightStatus.MissingPayloadStagingBytes;
        }

        return NativeAppEntryStagingPreflightStatus.Success;
    }

    private static NativeAppEntryStagingPreflightStatus MapDependencyStatus(
        NativeAppPackageStagingReadinessStatus status)
    {
        return status switch
        {
            NativeAppPackageStagingReadinessStatus.Success =>
                NativeAppEntryStagingPreflightStatus.Success,
            NativeAppPackageStagingReadinessStatus.ManifestLoadFailed =>
                NativeAppEntryStagingPreflightStatus.ManifestLoadFailed,
            NativeAppPackageStagingReadinessStatus.PackageParseFailed =>
                NativeAppEntryStagingPreflightStatus.PackageParseFailed,
            NativeAppPackageStagingReadinessStatus.LaunchPlanFailed =>
                NativeAppEntryStagingPreflightStatus.LaunchPlanFailed,
            NativeAppPackageStagingReadinessStatus.LaunchDescriptorFailed =>
                NativeAppEntryStagingPreflightStatus.LaunchDescriptorFailed,
            NativeAppPackageStagingReadinessStatus.PackageFileStagingFailed =>
                NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed,
            NativeAppPackageStagingReadinessStatus.PayloadMappingFailed =>
                NativeAppEntryStagingPreflightStatus.PayloadMappingFailed,
            NativeAppPackageStagingReadinessStatus.PayloadStagingFailed =>
                NativeAppEntryStagingPreflightStatus.PayloadStagingFailed,
            NativeAppPackageStagingReadinessStatus.ExecutableMemoryPlanFailed =>
                NativeAppEntryStagingPreflightStatus.ExecutableMemoryPlanFailed,
            NativeAppPackageStagingReadinessStatus.InvocationPlanFailed =>
                NativeAppEntryStagingPreflightStatus.InvocationPlanFailed,
            NativeAppPackageStagingReadinessStatus.PackageStagingByteMismatch =>
                NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed,
            NativeAppPackageStagingReadinessStatus.PayloadStagingByteMismatch =>
                NativeAppEntryStagingPreflightStatus.PayloadStagingFailed,
            NativeAppPackageStagingReadinessStatus.WritableStagingByteMismatch =>
                NativeAppEntryStagingPreflightStatus.ExecutableMemoryPlanFailed,
            NativeAppPackageStagingReadinessStatus.EntryOffsetMismatch =>
                NativeAppEntryStagingPreflightStatus.InvocationPlanFailed,
            _ => NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed
        };
    }
}
