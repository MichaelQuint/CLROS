using OS.Boot;
using OS.Storage;

namespace OS.Manifest;

public enum NativeAppSelectionLoadStatus
{
    Success = 0,
    ManifestLoadFailed = 1,
    PackageLoadFailed = 2,
    PackageParseFailed = 3,
    LaunchPlanFailed = 4,
    LaunchDescriptorFailed = 5
}

public readonly struct NativeAppSelectionLoadResult
{
    private readonly bool _isInitialized;

    private NativeAppSelectionLoadResult(
        NativeAppSelectionLoadStatus status,
        NativeManifestLoadResult manifestLoad,
        OsPackageLoadResult packageLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor)
    {
        _isInitialized = true;
        Status = status;
        ManifestLoad = manifestLoad;
        PackageLoad = packageLoad;
        PackageParse = packageParse;
        LaunchPlan = launchPlan;
        Descriptor = descriptor;
    }

    public NativeAppSelectionLoadStatus Status { get; }

    public NativeManifestLoadResult ManifestLoad { get; }

    public OsPackageLoadResult PackageLoad { get; }

    public GptPartitionTableSource ManifestGptTableSource => ManifestLoad.GptTableSource;

    public GptPartitionTableSource PackageGptTableSource => PackageLoad.GptTableSource;

    public OsPackageParseResult PackageParse { get; }

    public NativeAppLaunchPlanResult LaunchPlan { get; }

    public NativeAppLaunchDescriptorResult Descriptor { get; }

    public bool Succeeded =>
        _isInitialized &&
        Status == NativeAppSelectionLoadStatus.Success &&
        ManifestLoad.Succeeded &&
        PackageLoad.Succeeded &&
        PackageParse.Succeeded &&
        LaunchPlan.Succeeded &&
        Descriptor.Succeeded;

    public static NativeAppSelectionLoadResult Success(
        NativeManifestLoadResult manifestLoad,
        OsPackageLoadResult packageLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor)
    {
        return new NativeAppSelectionLoadResult(
            NativeAppSelectionLoadStatus.Success,
            manifestLoad,
            packageLoad,
            packageParse,
            launchPlan,
            descriptor);
    }

    public static NativeAppSelectionLoadResult Fail(
        NativeAppSelectionLoadStatus status,
        NativeManifestLoadResult manifestLoad,
        OsPackageLoadResult packageLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor)
    {
        return new NativeAppSelectionLoadResult(
            status,
            manifestLoad,
            packageLoad,
            packageParse,
            launchPlan,
            descriptor);
    }
}

public static class NativeAppSelectionLoader
{
    public static NativeAppSelectionLoadResult LoadFromPrimaryGptEspWithCallerBuffers(
        IReadOnlyBlockDevice? disk,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> manifestBuffer,
        Span<byte> packageBuffer,
        BootInfo bootInfo,
        NativeAppLaunchRuntimeCapabilities runtimeCapabilities)
    {
        NativeManifestLoadResult manifestLoad = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions,
            manifestBuffer);
        if (!manifestLoad.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(
                NativeAppSelectionLoadStatus.ManifestLoadFailed,
                manifestLoad,
                default,
                default,
                default,
                default);
        }

        OsPackageLoadResult packageLoad = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions,
            packageBuffer);
        if (!packageLoad.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(
                NativeAppSelectionLoadStatus.PackageLoadFailed,
                manifestLoad,
                packageLoad,
                PackageParseFromLoadFailure(packageLoad),
                default,
                default);
        }

        ReadOnlySpan<byte> packageSource = packageBuffer.Slice(0, checked((int)packageLoad.BytesRead));
        OsPackageParseResult packageParse = OsPackageParser.Parse(packageSource);
        if (!packageParse.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(
                NativeAppSelectionLoadStatus.PackageParseFailed,
                manifestLoad,
                packageLoad,
                packageParse,
                default,
                default);
        }

        ReadOnlySpan<byte> manifestSource = manifestBuffer.Slice(0, checked((int)manifestLoad.BytesRead));
        NativeAppLaunchPlanResult launchPlan = NativeAppLaunchPlan.Create(
            manifestSource,
            manifestLoad.Manifest,
            bootInfo,
            runtimeCapabilities);
        if (!launchPlan.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(
                NativeAppSelectionLoadStatus.LaunchPlanFailed,
                manifestLoad,
                packageLoad,
                packageParse,
                launchPlan,
                default);
        }

        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(
            manifestSource,
            packageSource,
            launchPlan);
        if (!descriptor.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(
                NativeAppSelectionLoadStatus.LaunchDescriptorFailed,
                manifestLoad,
                packageLoad,
                packageParse,
                launchPlan,
                descriptor);
        }

        return NativeAppSelectionLoadResult.Success(
            manifestLoad,
            packageLoad,
            packageParse,
            launchPlan,
            descriptor);
    }

    private static OsPackageParseResult PackageParseFromLoadFailure(OsPackageLoadResult packageLoad)
    {
        return packageLoad.Status == OsPackageLoadStatus.PackageParseFailed
            ? OsPackageParseResult.Fail(packageLoad.PackageStatus)
            : OsPackageParseResult.Fail(OsPackageStatus.MissingFile);
    }
}
