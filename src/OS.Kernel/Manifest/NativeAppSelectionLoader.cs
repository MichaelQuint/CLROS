using OS.Boot;
using OS.Storage;

namespace OS.Manifest;
public static class NativeAppSelectionLoader
{
    public static NativeAppSelectionLoadResult LoadFromPrimaryGptEspWithCallerBuffers(IReadOnlyBlockDevice? disk, Span<byte> primaryHeaderSector, Span<byte> partitionEntrySectors, Span<GptPartitionDescriptor> partitions, Span<byte> manifestBuffer, Span<byte> packageBuffer, BootInfo bootInfo, NativeAppLaunchRuntimeCapabilities runtimeCapabilities)
    {
        NativeManifestLoadResult manifestLoad = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(disk, primaryHeaderSector, partitionEntrySectors, partitions, manifestBuffer);
        if (!manifestLoad.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(NativeAppSelectionLoadStatus.ManifestLoadFailed, manifestLoad, default, default, default, default);
        }

        OsPackageLoadResult packageLoad = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(disk, primaryHeaderSector, partitionEntrySectors, partitions, packageBuffer);
        if (!packageLoad.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(NativeAppSelectionLoadStatus.PackageLoadFailed, manifestLoad, packageLoad, PackageParseFromLoadFailure(packageLoad), default, default);
        }

        ReadOnlySpan<byte> packageSource = packageBuffer.Slice(0, checked((int)packageLoad.BytesRead));
        OsPackageParseResult packageParse = OsPackageParser.Parse(packageSource);
        if (!packageParse.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(NativeAppSelectionLoadStatus.PackageParseFailed, manifestLoad, packageLoad, packageParse, default, default);
        }

        ReadOnlySpan<byte> manifestSource = manifestBuffer.Slice(0, checked((int)manifestLoad.BytesRead));
        NativeAppLaunchPlanResult launchPlan = NativeAppLaunchPlan.Create(manifestSource, manifestLoad.Manifest, bootInfo, runtimeCapabilities);
        if (!launchPlan.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(NativeAppSelectionLoadStatus.LaunchPlanFailed, manifestLoad, packageLoad, packageParse, launchPlan, default);
        }

        NativeAppLaunchDescriptorResult descriptor = NativeAppLaunchDescriptorBuilder.Create(manifestSource, packageSource, launchPlan);
        if (!descriptor.Succeeded)
        {
            return NativeAppSelectionLoadResult.Fail(NativeAppSelectionLoadStatus.LaunchDescriptorFailed, manifestLoad, packageLoad, packageParse, launchPlan, descriptor);
        }

        return NativeAppSelectionLoadResult.Success(manifestLoad, packageLoad, packageParse, launchPlan, descriptor);
    }

    private static OsPackageParseResult PackageParseFromLoadFailure(OsPackageLoadResult packageLoad)
    {
        return packageLoad.Status == OsPackageLoadStatus.PackageParseFailed ? OsPackageParseResult.Fail(packageLoad.PackageStatus) : OsPackageParseResult.Fail(OsPackageStatus.MissingFile);
    }
}
