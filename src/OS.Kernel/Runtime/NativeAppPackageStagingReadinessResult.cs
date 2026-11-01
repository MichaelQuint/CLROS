using OS.Manifest;
using OS.Storage;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppPackageStagingReadinessResult
{
    private NativeAppPackageStagingReadinessResult(NativeAppPackageStagingReadinessStatus status, NativeManifestLoadStatus manifestStatus, OsPackageStatus packageStatus, NativeAppLaunchStatus launchStatus, NativeAppLaunchDescriptorStatus descriptorStatus, OsPackageFileStagingStatus packageFileStagingStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppPayloadStagingStatus payloadStagingStatus, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppInvocationPlanStatus invocationStatus, bool isInitialized, uint appBinaryFileIndex, ulong packageFileBytesStaged, ulong payloadOffsetInPackage, ulong executableOffsetInPackage, ulong payloadExecutableBytesStaged, ulong requiredWritableStagingBytes, ulong requiredStackBytes, ulong entryOffsetInPackage)
    {
        Status = status;
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
        PayloadOffsetInPackage = payloadOffsetInPackage;
        ExecutableOffsetInPackage = executableOffsetInPackage;
        PayloadExecutableBytesStaged = payloadExecutableBytesStaged;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        RequiredStackBytes = requiredStackBytes;
        EntryOffsetInPackage = entryOffsetInPackage;
    }

    public NativeAppPackageStagingReadinessStatus Status { get; }
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
    public ulong PayloadOffsetInPackage { get; }
    public ulong ExecutableOffsetInPackage { get; }
    public ulong PayloadExecutableBytesStaged { get; }
    public ulong RequiredWritableStagingBytes { get; }
    public ulong RequiredStackBytes { get; }
    public ulong EntryOffsetInPackage { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppPackageStagingReadinessStatus.Success && PackageFileBytesStaged != 0 && PayloadExecutableBytesStaged != 0 && RequiredWritableStagingBytes != 0 && RequiredStackBytes != 0;

    public static NativeAppPackageStagingReadinessResult Success(NativeManifestLoadStatus manifestStatus, OsPackageStatus packageStatus, NativeAppLaunchStatus launchStatus, NativeAppLaunchDescriptorStatus descriptorStatus, OsPackageFileStagingStatus packageFileStagingStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppPayloadStagingStatus payloadStagingStatus, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppInvocationPlanStatus invocationStatus, uint appBinaryFileIndex, ulong packageFileBytesStaged, ulong payloadOffsetInPackage, ulong executableOffsetInPackage, ulong payloadExecutableBytesStaged, ulong requiredWritableStagingBytes, ulong requiredStackBytes, ulong entryOffsetInPackage)
    {
        return new NativeAppPackageStagingReadinessResult(NativeAppPackageStagingReadinessStatus.Success, manifestStatus, packageStatus, launchStatus, descriptorStatus, packageFileStagingStatus, payloadMappingStatus, payloadStagingStatus, executableMemoryStatus, invocationStatus, isInitialized: true, appBinaryFileIndex, packageFileBytesStaged, payloadOffsetInPackage, executableOffsetInPackage, payloadExecutableBytesStaged, requiredWritableStagingBytes, requiredStackBytes, entryOffsetInPackage);
    }

    public static NativeAppPackageStagingReadinessResult Fail(NativeAppPackageStagingReadinessStatus status, NativeManifestLoadStatus manifestStatus, OsPackageStatus packageStatus, NativeAppLaunchStatus launchStatus, NativeAppLaunchDescriptorStatus descriptorStatus, OsPackageFileStagingStatus packageFileStagingStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppPayloadStagingStatus payloadStagingStatus, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppInvocationPlanStatus invocationStatus, uint appBinaryFileIndex, ulong packageFileBytesStaged, ulong payloadOffsetInPackage, ulong executableOffsetInPackage, ulong payloadExecutableBytesStaged, ulong requiredWritableStagingBytes, ulong requiredStackBytes, ulong entryOffsetInPackage)
    {
        return new NativeAppPackageStagingReadinessResult(status, manifestStatus, packageStatus, launchStatus, descriptorStatus, packageFileStagingStatus, payloadMappingStatus, payloadStagingStatus, executableMemoryStatus, invocationStatus, isInitialized: true, appBinaryFileIndex, packageFileBytesStaged, payloadOffsetInPackage, executableOffsetInPackage, payloadExecutableBytesStaged, requiredWritableStagingBytes, requiredStackBytes, entryOffsetInPackage);
    }
}
