using System.Runtime.CompilerServices;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static class NativeAppEntryStagingPreflightTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel entry staging preflight accepts staged package and payload", TestAcceptsStaging, ref failures);
        Run("appmodel entry staging preflight rejects payload staging failure", TestRejectsPayloadStagingFailure, ref failures);
        Run("appmodel entry staging preflight rejects default staging readiness", TestRejectsDefaultStagingReadiness, ref failures);
        Run("appmodel entry staging preflight default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("appmodel entry staging preflight preserves no-launch boundary", TestPreservesNoLaunchBoundary, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app entry staging preflight host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcceptsStaging()
    {
        NativeAppEntryStagingPreflightResult result =
            NativeAppEntryStagingPreflight.FromPackageStagingReadiness(CreateReadyReadiness());

        return result.Succeeded
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
            && result.EntryOffsetInPackage == 4136;
    }

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

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppEntryStagingPreflightStatus.PayloadStagingFailed
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.PayloadStagingFailed
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.DestinationTooSmall
            && result.PackageFileBytesStaged == 512
            && result.PayloadExecutableBytesStaged == 0;
    }

    private static bool TestRejectsDefaultStagingReadiness()
    {
        NativeAppPackageStagingReadinessResult readiness = default;
        NativeAppEntryStagingPreflightResult result =
            NativeAppEntryStagingPreflight.FromPackageStagingReadiness(readiness);

        return !readiness.IsInitialized
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppEntryStagingPreflightStatus.NotInitialized
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success
            && result.PackageFileBytesStaged == 0
            && result.PayloadExecutableBytesStaged == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppEntryStagingPreflightResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppEntryStagingPreflightStatus.Success
            && result.StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success
            && result.ManifestStatus == NativeManifestLoadStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.LaunchStatus == NativeAppLaunchStatus.Success
            && result.DescriptorStatus == NativeAppLaunchDescriptorStatus.Success
            && result.PackageFileStagingStatus == OsPackageFileStagingStatus.Success
            && result.PayloadStagingStatus == NativeAppPayloadStagingStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppEntryStagingPreflightResult>();
    }

    private static bool TestPreservesNoLaunchBoundary()
    {
        return NativeAppEntryStagingPreflight.RequiresPackageStagingReadiness
            && NativeAppEntryStagingPreflight.RequiresCallerOwnedPackageFileStaging
            && NativeAppEntryStagingPreflight.RequiresCallerOwnedPayloadStaging
            && !NativeAppEntryStagingPreflight.ReadsLiveStorage
            && !NativeAppEntryStagingPreflight.AllocatesMemory
            && !NativeAppEntryStagingPreflight.MapsExecutablePages
            && !NativeAppEntryStagingPreflight.ChangesPagePermissions
            && !NativeAppEntryStagingPreflight.ExecutesAppCode
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppEntryStagingPreflightResult>();
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
