using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static class NativeAppEntryStagingPreflightTests
{

    [HostTest("appmodel entry staging preflight accepts staged package and payload", HostLane.Fast, 0)]
    private static bool TestAcceptsStaging()
    {
        NativeAppEntryStagingPreflightResult result =
            NativeAppEntryStagingPreflight.FromPackageStagingReadiness(CreateReadyReadiness());

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppEntryStagingPreflightStatus.Success
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success
            && result.ManifestStatus == NativeManifestLoadStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadMappingStatus == NativeAppPayloadMappingPlanStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.Success
            && result.ExecutableMemoryStatus == NativeAppExecutableMemoryPlanStatus.Success
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.AppBinaryFileIndex == 2
            && result.PackageFileBytesStaged == 512
            && result.PayloadExecutableBytesStaged == 480
            && result.RequiredWritableStagingBytes == 4096
            && result.RequiredStackBytes == NativeAppInvocationPlanner.DefaultStackBytes
            && result.EntryOffsetInPackage == 4136);
    }

    [HostTest("appmodel entry staging preflight rejects payload staging failure", HostLane.Fast, 1)]
    private static bool TestRejectsPayloadStagingFailure()
    {
        NativeAppPackageStagingReadinessResult readiness =
            NativeAppPackageStagingReadinessResult.Fail(
                NativeAppPackageStagingReadinessStatus.PayloadStagingFailed,
                NativeManifestLoadStatus.Success,
                OsPackageStatus.Success,
                NativeAppLaunchStatus.Success,
                NativeAppLaunchDescriptorStatus.Success,
                OsPackageFileStagingStatus.Success,
                NativeAppPayloadMappingPlanStatus.Success,
                NativeAppPayloadStagingStatus.DestinationTooSmall,
                NativeAppExecutableMemoryPlanStatus.Success,
                NativeAppInvocationPlanStatus.Success,
                appBinaryFileIndex: 2,
                packageFileBytesStaged: 512,
                payloadOffsetInPackage: 4096,
                executableOffsetInPackage: 4128,
                payloadExecutableBytesStaged: 0,
                requiredWritableStagingBytes: 4096,
                requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes,
                entryOffsetInPackage: 4136);

        NativeAppEntryStagingPreflightResult result =
            NativeAppEntryStagingPreflight.FromPackageStagingReadiness(readiness);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppEntryStagingPreflightStatus.PayloadStagingFailed
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.PayloadStagingFailed
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.DestinationTooSmall
            && result.PackageFileBytesStaged == 512
            && result.PayloadExecutableBytesStaged == 0);
    }

    [HostTest("appmodel entry staging preflight rejects default staging readiness", HostLane.Fast, 2)]
    private static bool TestRejectsDefaultStagingReadiness()
    {
        NativeAppPackageStagingReadinessResult readiness = default;
        NativeAppEntryStagingPreflightResult result =
            NativeAppEntryStagingPreflight.FromPackageStagingReadiness(readiness);

        return HostCheck.Result(!readiness.IsInitialized
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppEntryStagingPreflightStatus.NotInitialized
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success
            && result.PackageFileBytesStaged == 0
            && result.PayloadExecutableBytesStaged == 0);
    }

    [HostTest("appmodel entry staging preflight default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppEntryStagingPreflightResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppEntryStagingPreflightStatus.Success
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success
            && result.ManifestStatus == NativeManifestLoadStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppEntryStagingPreflightResult>());
    }

    [HostTest("appmodel entry staging preflight preserves no-launch boundary", HostLane.Fast, 4)]
    private static bool TestPreservesNoLaunchBoundary()
    {
        return HostCheck.Result(NativeAppEntryStagingPreflight.RequiresPackageStagingReadiness
            && NativeAppEntryStagingPreflight.RequiresCallerOwnedPackageFileStaging
            && NativeAppEntryStagingPreflight.RequiresCallerOwnedPayloadStaging
            && !NativeAppEntryStagingPreflight.ReadsLiveStorage
            && !NativeAppEntryStagingPreflight.AllocatesMemory
            && !NativeAppEntryStagingPreflight.MapsExecutablePages
            && !NativeAppEntryStagingPreflight.ChangesPagePermissions
            && !NativeAppEntryStagingPreflight.ExecutesAppCode
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppEntryStagingPreflightResult>());
    }

    private static NativeAppPackageStagingReadinessResult CreateReadyReadiness()
    {
        return NativeAppPackageStagingReadinessResult.Success(
            NativeManifestLoadStatus.Success,
            OsPackageStatus.Success,
            NativeAppLaunchStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            OsPackageFileStagingStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppPayloadStagingStatus.Success,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppInvocationPlanStatus.Success,
            appBinaryFileIndex: 2,
            packageFileBytesStaged: 512,
            payloadOffsetInPackage: 4096,
            executableOffsetInPackage: 4128,
            payloadExecutableBytesStaged: 480,
            requiredWritableStagingBytes: 4096,
            requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes,
            entryOffsetInPackage: 4136);
    }
}
