using OS.Kernel.Runtime;
using OS.Storage;

namespace OS.Manifest;

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
    public static NativeAppEntryStagingPreflightResult FromPackageStagingReadiness(NativeAppPackageStagingReadinessResult readiness)
    {
        NativeAppEntryStagingPreflightStatus status = GetStatus(readiness);
        return status == NativeAppEntryStagingPreflightStatus.Success ? NativeAppEntryStagingPreflightResult.Success(readiness.Status, readiness.ManifestStatus, readiness.PackageStatus, readiness.LaunchStatus, readiness.DescriptorStatus, readiness.PackageFileStagingStatus, readiness.PayloadMappingStatus, readiness.PayloadStagingStatus, readiness.ExecutableMemoryStatus, readiness.InvocationStatus, readiness.AppBinaryFileIndex, readiness.PackageFileBytesStaged, readiness.PayloadExecutableBytesStaged, readiness.RequiredWritableStagingBytes, readiness.RequiredStackBytes, readiness.EntryOffsetInPackage) : NativeAppEntryStagingPreflightResult.Fail(status, readiness.Status, readiness.ManifestStatus, readiness.PackageStatus, readiness.LaunchStatus, readiness.DescriptorStatus, readiness.PackageFileStagingStatus, readiness.PayloadMappingStatus, readiness.PayloadStagingStatus, readiness.ExecutableMemoryStatus, readiness.InvocationStatus, readiness.AppBinaryFileIndex, readiness.PackageFileBytesStaged, readiness.PayloadExecutableBytesStaged, readiness.RequiredWritableStagingBytes, readiness.RequiredStackBytes, readiness.EntryOffsetInPackage);
    }

    private static NativeAppEntryStagingPreflightStatus GetStatus(NativeAppPackageStagingReadinessResult readiness)
    {
        if (!readiness.IsInitialized)
        {
            return NativeAppEntryStagingPreflightStatus.NotInitialized;
        }

        NativeAppEntryStagingPreflightStatus dependencyStatus = MapDependencyStatus(readiness.Status);
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

    private static NativeAppEntryStagingPreflightStatus MapDependencyStatus(NativeAppPackageStagingReadinessStatus status)
    {
        return status switch
        {
            NativeAppPackageStagingReadinessStatus.Success => NativeAppEntryStagingPreflightStatus.Success,
            NativeAppPackageStagingReadinessStatus.ManifestLoadFailed => NativeAppEntryStagingPreflightStatus.ManifestLoadFailed,
            NativeAppPackageStagingReadinessStatus.PackageParseFailed => NativeAppEntryStagingPreflightStatus.PackageParseFailed,
            NativeAppPackageStagingReadinessStatus.LaunchPlanFailed => NativeAppEntryStagingPreflightStatus.LaunchPlanFailed,
            NativeAppPackageStagingReadinessStatus.LaunchDescriptorFailed => NativeAppEntryStagingPreflightStatus.LaunchDescriptorFailed,
            NativeAppPackageStagingReadinessStatus.PackageFileStagingFailed => NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed,
            NativeAppPackageStagingReadinessStatus.PayloadMappingFailed => NativeAppEntryStagingPreflightStatus.PayloadMappingFailed,
            NativeAppPackageStagingReadinessStatus.PayloadStagingFailed => NativeAppEntryStagingPreflightStatus.PayloadStagingFailed,
            NativeAppPackageStagingReadinessStatus.ExecutableMemoryPlanFailed => NativeAppEntryStagingPreflightStatus.ExecutableMemoryPlanFailed,
            NativeAppPackageStagingReadinessStatus.InvocationPlanFailed => NativeAppEntryStagingPreflightStatus.InvocationPlanFailed,
            NativeAppPackageStagingReadinessStatus.PackageStagingByteMismatch => NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed,
            NativeAppPackageStagingReadinessStatus.PayloadStagingByteMismatch => NativeAppEntryStagingPreflightStatus.PayloadStagingFailed,
            NativeAppPackageStagingReadinessStatus.WritableStagingByteMismatch => NativeAppEntryStagingPreflightStatus.ExecutableMemoryPlanFailed,
            NativeAppPackageStagingReadinessStatus.EntryOffsetMismatch => NativeAppEntryStagingPreflightStatus.InvocationPlanFailed,
            _ => NativeAppEntryStagingPreflightStatus.PackageFileStagingFailed
        };
    }
}
