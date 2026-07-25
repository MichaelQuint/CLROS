using OS.Manifest;
using OS.Storage;

namespace OS.Kernel.Runtime;

public enum NativeAppPackageStagingReadinessStatus
{
    Success = 0,
    ManifestLoadFailed = 1,
    PackageParseFailed = 2,
    LaunchPlanFailed = 3,
    LaunchDescriptorFailed = 4,
    PackageFileStagingFailed = 5,
    PayloadMappingFailed = 6,
    PayloadStagingFailed = 7,
    ExecutableMemoryPlanFailed = 8,
    InvocationPlanFailed = 9,
    PackageStagingByteMismatch = 10,
    PayloadStagingByteMismatch = 11,
    WritableStagingByteMismatch = 12,
    EntryOffsetMismatch = 13
}

public readonly struct NativeAppPackageStagingReadinessResult
{
    private NativeAppPackageStagingReadinessResult(
        NativeAppPackageStagingReadinessStatus status,
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
        ulong payloadOffsetInPackage,
        ulong executableOffsetInPackage,
        ulong payloadExecutableBytesStaged,
        ulong requiredWritableStagingBytes,
        ulong requiredStackBytes,
        ulong entryOffsetInPackage)
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

    public bool Succeeded =>
        IsInitialized &&
        Status == NativeAppPackageStagingReadinessStatus.Success &&
        PackageFileBytesStaged != 0 &&
        PayloadExecutableBytesStaged != 0 &&
        RequiredWritableStagingBytes != 0 &&
        RequiredStackBytes != 0;

    public static NativeAppPackageStagingReadinessResult Success(
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
        ulong payloadOffsetInPackage,
        ulong executableOffsetInPackage,
        ulong payloadExecutableBytesStaged,
        ulong requiredWritableStagingBytes,
        ulong requiredStackBytes,
        ulong entryOffsetInPackage)
    {
        return new NativeAppPackageStagingReadinessResult(
            NativeAppPackageStagingReadinessStatus.Success,
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
            payloadOffsetInPackage,
            executableOffsetInPackage,
            payloadExecutableBytesStaged,
            requiredWritableStagingBytes,
            requiredStackBytes,
            entryOffsetInPackage);
    }

    public static NativeAppPackageStagingReadinessResult Fail(
        NativeAppPackageStagingReadinessStatus status,
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
        ulong payloadOffsetInPackage,
        ulong executableOffsetInPackage,
        ulong payloadExecutableBytesStaged,
        ulong requiredWritableStagingBytes,
        ulong requiredStackBytes,
        ulong entryOffsetInPackage)
    {
        return new NativeAppPackageStagingReadinessResult(
            status,
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
            payloadOffsetInPackage,
            executableOffsetInPackage,
            payloadExecutableBytesStaged,
            requiredWritableStagingBytes,
            requiredStackBytes,
            entryOffsetInPackage);
    }
}

public static class NativeAppPackageStagingReadinessPlanner
{
    public const bool RequiresManifestPackageAndPayloadResults = true;
    public const bool RequiresCallerOwnedPackageFileStaging = true;
    public const bool RequiresCallerOwnedPayloadStaging = true;
    public const bool ReadsLiveStorage = false;
    public const bool AllocatesMemory = false;
    public const bool MapsExecutablePages = false;
    public const bool ChangesPagePermissions = false;
    public const bool RelocatesExecutable = false;
    public const bool ExecutesAppCode = false;

    public static NativeAppPackageStagingReadinessResult Compose(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor,
        OsPackageFileStagingResult packageFileStaging,
        NativeAppPayloadMappingPlanResult payloadMapping,
        NativeAppPayloadStagingResult payloadStaging,
        NativeAppExecutableMemoryPlanResult executableMemory,
        NativeAppInvocationPlanResult invocation)
    {
        NativeAppPackageStagingReadinessResult evidence = CreateEvidence(
            NativeAppPackageStagingReadinessStatus.Success,
            manifestLoad,
            packageParse,
            launchPlan,
            descriptor,
            packageFileStaging,
            payloadMapping,
            payloadStaging,
            executableMemory,
            invocation);

        if (!manifestLoad.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.ManifestLoadFailed);
        }

        if (!packageParse.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.PackageParseFailed);
        }

        if (!launchPlan.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.LaunchPlanFailed);
        }

        if (!descriptor.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.LaunchDescriptorFailed);
        }

        if (!packageFileStaging.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.PackageFileStagingFailed);
        }

        if (!payloadMapping.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.PayloadMappingFailed);
        }

        if (!payloadStaging.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.PayloadStagingFailed);
        }

        if (!executableMemory.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.ExecutableMemoryPlanFailed);
        }

        if (!invocation.Succeeded)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.InvocationPlanFailed);
        }

        if (packageFileStaging.BytesWritten != descriptor.Descriptor.AppBinaryLength ||
            payloadMapping.Plan.PayloadLength != descriptor.Descriptor.AppBinaryLength)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.PackageStagingByteMismatch);
        }

        if (payloadStaging.BytesWritten != payloadMapping.Plan.ExecutableLength ||
            payloadStaging.BytesWritten != executableMemory.ExecutableBytes)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.PayloadStagingByteMismatch);
        }

        if (executableMemory.RequiredWritableStagingBytes != invocation.Plan.RequiredWritableStagingBytes)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.WritableStagingByteMismatch);
        }

        if (payloadMapping.Plan.EntryOffsetInPackage != invocation.Plan.EntryOffsetInPackage)
        {
            return WithStatus(evidence, NativeAppPackageStagingReadinessStatus.EntryOffsetMismatch);
        }

        return evidence;
    }

    private static NativeAppPackageStagingReadinessResult CreateEvidence(
        NativeAppPackageStagingReadinessStatus status,
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor,
        OsPackageFileStagingResult packageFileStaging,
        NativeAppPayloadMappingPlanResult payloadMapping,
        NativeAppPayloadStagingResult payloadStaging,
        NativeAppExecutableMemoryPlanResult executableMemory,
        NativeAppInvocationPlanResult invocation)
    {
        uint appBinaryFileIndex = descriptor.Succeeded
            ? descriptor.Descriptor.AppBinary.Index
            : packageFileStaging.FileIndex;
        ulong packageFileBytes = packageFileStaging.BytesWritten;
        ulong payloadOffsetInPackage = payloadMapping.Succeeded
            ? payloadMapping.Plan.PayloadOffsetInPackage
            : payloadStaging.PayloadOffsetInPackage;
        ulong executableOffsetInPackage = payloadStaging.ExecutableOffsetInPackage != 0
            ? payloadStaging.ExecutableOffsetInPackage
            : payloadMapping.Plan.ExecutableOffsetInPackage;
        ulong payloadExecutableBytes = payloadStaging.BytesWritten;
        ulong requiredWritableStagingBytes = executableMemory.RequiredWritableStagingBytes;
        ulong requiredStackBytes = invocation.Succeeded
            ? invocation.Plan.RequiredStackBytes
            : 0;
        ulong entryOffsetInPackage = invocation.Succeeded
            ? invocation.Plan.EntryOffsetInPackage
            : payloadMapping.Plan.EntryOffsetInPackage;

        return status == NativeAppPackageStagingReadinessStatus.Success
            ? NativeAppPackageStagingReadinessResult.Success(
                manifestLoad.Status,
                packageParse.Status,
                launchPlan.Status,
                descriptor.Status,
                packageFileStaging.Status,
                payloadMapping.Status,
                payloadStaging.Status,
                executableMemory.Status,
                invocation.Status,
                appBinaryFileIndex,
                packageFileBytes,
                payloadOffsetInPackage,
                executableOffsetInPackage,
                payloadExecutableBytes,
                requiredWritableStagingBytes,
                requiredStackBytes,
                entryOffsetInPackage)
            : NativeAppPackageStagingReadinessResult.Fail(
                status,
                manifestLoad.Status,
                packageParse.Status,
                launchPlan.Status,
                descriptor.Status,
                packageFileStaging.Status,
                payloadMapping.Status,
                payloadStaging.Status,
                executableMemory.Status,
                invocation.Status,
                appBinaryFileIndex,
                packageFileBytes,
                payloadOffsetInPackage,
                executableOffsetInPackage,
                payloadExecutableBytes,
                requiredWritableStagingBytes,
                requiredStackBytes,
                entryOffsetInPackage);
    }

    private static NativeAppPackageStagingReadinessResult WithStatus(
        NativeAppPackageStagingReadinessResult result,
        NativeAppPackageStagingReadinessStatus status)
    {
        return NativeAppPackageStagingReadinessResult.Fail(
            status,
            result.ManifestStatus,
            result.PackageStatus,
            result.LaunchStatus,
            result.DescriptorStatus,
            result.PackageFileStagingStatus,
            result.PayloadMappingStatus,
            result.PayloadStagingStatus,
            result.ExecutableMemoryStatus,
            result.InvocationStatus,
            result.AppBinaryFileIndex,
            result.PackageFileBytesStaged,
            result.PayloadOffsetInPackage,
            result.ExecutableOffsetInPackage,
            result.PayloadExecutableBytesStaged,
            result.RequiredWritableStagingBytes,
            result.RequiredStackBytes,
            result.EntryOffsetInPackage);
    }
}
