using OS.Storage;

namespace OS.Manifest;
public static class NativeAppLaunchDescriptorBuilder
{
    public static NativeAppLaunchDescriptorResult Create(ReadOnlySpan<byte> manifestSource, ReadOnlySpan<byte> package, NativeAppLaunchPlanResult launchPlan)
    {
        if (!launchPlan.Succeeded)
        {
            return NativeAppLaunchDescriptorResult.Fail(NativeAppLaunchDescriptorStatus.LaunchPlanFailed, launchPlan.Status, OsPackageFileSystemStatus.Success, OsPackageStatus.Success);
        }

        if (!launchPlan.Plan.TryGetAppPath(manifestSource, out ReadOnlySpan<byte> appPath) || appPath.Length == 0)
        {
            return NativeAppLaunchDescriptorResult.Fail(NativeAppLaunchDescriptorStatus.AppPathUnavailable, NativeAppLaunchStatus.InvalidAppPathField, OsPackageFileSystemStatus.Success, OsPackageStatus.Success);
        }

        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, appPath);
        if (!open.Succeeded)
        {
            return NativeAppLaunchDescriptorResult.Fail(MapOpenStatus(open.Status), NativeAppLaunchStatus.Success, open.Status, open.PackageStatus);
        }

        ReadOnlySpan<byte> appPayload = package.Slice((int)open.Entry.ContentOffset, (int)open.Entry.ContentLength);
        NativeAppPayloadHeaderResult payloadHeader = NativeAppPayloadHeader.Parse(appPayload);
        if (!payloadHeader.Succeeded)
        {
            return NativeAppLaunchDescriptorResult.Fail(NativeAppLaunchDescriptorStatus.InvalidPayloadHeader, NativeAppLaunchStatus.Success, OsPackageFileSystemStatus.Success, OsPackageStatus.Success, payloadHeader.Status);
        }

        NativeAppLaunchDescriptor descriptor = new NativeAppLaunchDescriptor(launchPlan.Plan, open.Entry, open.FileLength, launchPlan.Plan.RequestedNetworkMode, launchPlan.Plan.LogMode, launchPlan.Plan.ArgumentByteLength, payloadHeader.Header);
        return NativeAppLaunchDescriptorResult.Success(descriptor);
    }

    private static NativeAppLaunchDescriptorStatus MapOpenStatus(OsPackageFileSystemStatus status)
    {
        return status switch
        {
            OsPackageFileSystemStatus.MissingFile => NativeAppLaunchDescriptorStatus.PackageFileMissing,
            OsPackageFileSystemStatus.InvalidPath => NativeAppLaunchDescriptorStatus.PackageFileInvalidPath,
            _ => NativeAppLaunchDescriptorStatus.PackageFileSystemFailed
        };
    }
}
