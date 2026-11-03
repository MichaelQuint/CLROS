using OS.Boot;
using OS.Storage;

namespace OS.Manifest;

public readonly struct NativeAppSelectionLoadResult
{
    private readonly bool _isInitialized;
    private NativeAppSelectionLoadResult(NativeAppSelectionLoadStatus status, NativeManifestLoadResult manifestLoad, OsPackageLoadResult packageLoad, OsPackageParseResult packageParse, NativeAppLaunchPlanResult launchPlan, NativeAppLaunchDescriptorResult descriptor)
    {
        _isInitialized = true;
        Status = status;
        ManifestLoad = manifestLoad;
        PackageLoad = packageLoad;
        PackageParse = packageParse;
        LaunchPlan = launchPlan;
        Descriptor = descriptor;
    }

    public NativeAppSelectionLoadStatus Status
    {
        get;
    }
    public NativeManifestLoadResult ManifestLoad
    {
        get;
    }
    public OsPackageLoadResult PackageLoad
    {
        get;
    }
    public GptPartitionTableSource ManifestGptTableSource => ManifestLoad.GptTableSource;
    public GptPartitionTableSource PackageGptTableSource => PackageLoad.GptTableSource;
    public OsPackageParseResult PackageParse
    {
        get;
    }
    public NativeAppLaunchPlanResult LaunchPlan
    {
        get;
    }
    public NativeAppLaunchDescriptorResult Descriptor
    {
        get;
    }
    public bool Succeeded => _isInitialized && Status == NativeAppSelectionLoadStatus.Success && ManifestLoad.Succeeded && PackageLoad.Succeeded && PackageParse.Succeeded && LaunchPlan.Succeeded && Descriptor.Succeeded;

    public static NativeAppSelectionLoadResult Success(NativeManifestLoadResult manifestLoad, OsPackageLoadResult packageLoad, OsPackageParseResult packageParse, NativeAppLaunchPlanResult launchPlan, NativeAppLaunchDescriptorResult descriptor)
    {
        return new NativeAppSelectionLoadResult(NativeAppSelectionLoadStatus.Success, manifestLoad, packageLoad, packageParse, launchPlan, descriptor);
    }

    public static NativeAppSelectionLoadResult Fail(NativeAppSelectionLoadStatus status, NativeManifestLoadResult manifestLoad, OsPackageLoadResult packageLoad, OsPackageParseResult packageParse, NativeAppLaunchPlanResult launchPlan, NativeAppLaunchDescriptorResult descriptor)
    {
        return new NativeAppSelectionLoadResult(status, manifestLoad, packageLoad, packageParse, launchPlan, descriptor);
    }
}
