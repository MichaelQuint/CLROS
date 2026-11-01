using OS.Storage;

namespace OS.Manifest;
public readonly struct NativeAppLaunchDescriptorResult
{
    private NativeAppLaunchDescriptorResult(NativeAppLaunchDescriptorStatus status, NativeAppLaunchStatus launchStatus, OsPackageFileSystemStatus packageFileStatus, OsPackageStatus packageStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, NativeAppLaunchDescriptor descriptor, bool isInitialized)
    {
        Status = status;
        LaunchStatus = launchStatus;
        PackageFileStatus = packageFileStatus;
        PackageStatus = packageStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        Descriptor = descriptor;
        IsInitialized = isInitialized;
    }

    public NativeAppLaunchDescriptorStatus Status { get; }
    public bool IsInitialized { get; }
    public NativeAppLaunchStatus LaunchStatus { get; }
    public OsPackageFileSystemStatus PackageFileStatus { get; }
    public OsPackageStatus PackageStatus { get; }
    public NativeAppPayloadHeaderStatus PayloadHeaderStatus { get; }
    public NativeAppLaunchDescriptor Descriptor { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppLaunchDescriptorStatus.Success;

    public static NativeAppLaunchDescriptorResult Success(NativeAppLaunchDescriptor descriptor)
    {
        return new NativeAppLaunchDescriptorResult(NativeAppLaunchDescriptorStatus.Success, NativeAppLaunchStatus.Success, OsPackageFileSystemStatus.Success, OsPackageStatus.Success, NativeAppPayloadHeaderStatus.Success, descriptor, isInitialized: true);
    }

    public static NativeAppLaunchDescriptorResult Fail(NativeAppLaunchDescriptorStatus status, NativeAppLaunchStatus launchStatus, OsPackageFileSystemStatus packageFileStatus, OsPackageStatus packageStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus = NativeAppPayloadHeaderStatus.Success)
    {
        return new NativeAppLaunchDescriptorResult(status, launchStatus, packageFileStatus, packageStatus, payloadHeaderStatus, default, isInitialized: true);
    }
}
