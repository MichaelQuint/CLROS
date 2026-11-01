using OS.Kernel.Runtime;
using OS.Storage;

namespace OS.Manifest;
public readonly struct NativeAppEntryStagingPreflightResult
{
    private NativeAppEntryStagingPreflightResult(NativeAppEntryStagingPreflightStatus status, NativeAppPackageStagingReadinessStatus stagingReadinessStatus, NativeManifestLoadStatus manifestStatus, OsPackageStatus packageStatus, NativeAppLaunchStatus launchStatus, NativeAppLaunchDescriptorStatus descriptorStatus, OsPackageFileStagingStatus packageFileStagingStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppPayloadStagingStatus payloadStagingStatus, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppInvocationPlanStatus invocationStatus, bool isInitialized, uint appBinaryFileIndex, ulong packageFileBytesStaged, ulong payloadExecutableBytesStaged, ulong requiredWritableStagingBytes, ulong requiredStackBytes, ulong entryOffsetInPackage)
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
    public bool Succeeded => IsInitialized && Status == NativeAppEntryStagingPreflightStatus.Success && StagingReadinessStatus == NativeAppPackageStagingReadinessStatus.Success && PackageFileBytesStaged != 0 && PayloadExecutableBytesStaged != 0 && RequiredWritableStagingBytes != 0 && RequiredStackBytes != 0;

    public static NativeAppEntryStagingPreflightResult Success(NativeAppPackageStagingReadinessStatus stagingReadinessStatus, NativeManifestLoadStatus manifestStatus, OsPackageStatus packageStatus, NativeAppLaunchStatus launchStatus, NativeAppLaunchDescriptorStatus descriptorStatus, OsPackageFileStagingStatus packageFileStagingStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppPayloadStagingStatus payloadStagingStatus, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppInvocationPlanStatus invocationStatus, uint appBinaryFileIndex, ulong packageFileBytesStaged, ulong payloadExecutableBytesStaged, ulong requiredWritableStagingBytes, ulong requiredStackBytes, ulong entryOffsetInPackage)
    {
        return new NativeAppEntryStagingPreflightResult(NativeAppEntryStagingPreflightStatus.Success, stagingReadinessStatus, manifestStatus, packageStatus, launchStatus, descriptorStatus, packageFileStagingStatus, payloadMappingStatus, payloadStagingStatus, executableMemoryStatus, invocationStatus, isInitialized: true, appBinaryFileIndex, packageFileBytesStaged, payloadExecutableBytesStaged, requiredWritableStagingBytes, requiredStackBytes, entryOffsetInPackage);
    }

    public static NativeAppEntryStagingPreflightResult Fail(NativeAppEntryStagingPreflightStatus status, NativeAppPackageStagingReadinessStatus stagingReadinessStatus, NativeManifestLoadStatus manifestStatus, OsPackageStatus packageStatus, NativeAppLaunchStatus launchStatus, NativeAppLaunchDescriptorStatus descriptorStatus, OsPackageFileStagingStatus packageFileStagingStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppPayloadStagingStatus payloadStagingStatus, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppInvocationPlanStatus invocationStatus, uint appBinaryFileIndex, ulong packageFileBytesStaged, ulong payloadExecutableBytesStaged, ulong requiredWritableStagingBytes, ulong requiredStackBytes, ulong entryOffsetInPackage)
    {
        return new NativeAppEntryStagingPreflightResult(status, stagingReadinessStatus, manifestStatus, packageStatus, launchStatus, descriptorStatus, packageFileStagingStatus, payloadMappingStatus, payloadStagingStatus, executableMemoryStatus, invocationStatus, isInitialized: true, appBinaryFileIndex, packageFileBytesStaged, payloadExecutableBytesStaged, requiredWritableStagingBytes, requiredStackBytes, entryOffsetInPackage);
    }
}
