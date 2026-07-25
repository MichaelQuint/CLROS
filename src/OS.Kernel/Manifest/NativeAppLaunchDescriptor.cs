using OS.Storage;

namespace OS.Manifest;

public enum NativeAppLaunchDescriptorStatus
{
    Success = 0,
    LaunchPlanFailed = 1,
    AppPathUnavailable = 2,
    PackageFileMissing = 3,
    PackageFileInvalidPath = 4,
    PackageFileSystemFailed = 5,
    InvalidPayloadHeader = 6
}

public readonly struct NativeAppLaunchDescriptor
{
    public NativeAppLaunchDescriptor(
        NativeAppLaunchPlan plan,
        OsPackageFileEntry appBinary,
        ulong appBinaryLength,
        NativeManifestNetworkMode networkMode,
        NativeManifestLogMode logMode,
        int argumentByteLength,
        NativeAppPayloadHeader payloadHeader)
    {
        Plan = plan;
        AppBinary = appBinary;
        AppBinaryLength = appBinaryLength;
        NetworkMode = networkMode;
        LogMode = logMode;
        ArgumentByteLength = argumentByteLength;
        PayloadHeader = payloadHeader;
    }

    public NativeAppLaunchPlan Plan { get; }

    public OsPackageFileEntry AppBinary { get; }

    public ulong AppBinaryLength { get; }

    public NativeManifestNetworkMode NetworkMode { get; }

    public NativeManifestLogMode LogMode { get; }

    public int ArgumentByteLength { get; }

    public NativeAppPayloadHeader PayloadHeader { get; }
}

public readonly struct NativeAppLaunchDescriptorResult
{
    private NativeAppLaunchDescriptorResult(
        NativeAppLaunchDescriptorStatus status,
        NativeAppLaunchStatus launchStatus,
        OsPackageFileSystemStatus packageFileStatus,
        OsPackageStatus packageStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        NativeAppLaunchDescriptor descriptor,
        bool isInitialized)
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
        return new NativeAppLaunchDescriptorResult(
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppLaunchStatus.Success,
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            descriptor,
            isInitialized: true);
    }

    public static NativeAppLaunchDescriptorResult Fail(
        NativeAppLaunchDescriptorStatus status,
        NativeAppLaunchStatus launchStatus,
        OsPackageFileSystemStatus packageFileStatus,
        OsPackageStatus packageStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus = NativeAppPayloadHeaderStatus.Success)
    {
        return new NativeAppLaunchDescriptorResult(
            status,
            launchStatus,
            packageFileStatus,
            packageStatus,
            payloadHeaderStatus,
            default,
            isInitialized: true);
    }
}

public static class NativeAppLaunchDescriptorBuilder
{
    public static NativeAppLaunchDescriptorResult Create(
        ReadOnlySpan<byte> manifestSource,
        ReadOnlySpan<byte> package,
        NativeAppLaunchPlanResult launchPlan)
    {
        if (!launchPlan.Succeeded)
        {
            return NativeAppLaunchDescriptorResult.Fail(
                NativeAppLaunchDescriptorStatus.LaunchPlanFailed,
                launchPlan.Status,
                OsPackageFileSystemStatus.Success,
                OsPackageStatus.Success);
        }

        if (!launchPlan.Plan.TryGetAppPath(manifestSource, out ReadOnlySpan<byte> appPath)
            || appPath.Length == 0)
        {
            return NativeAppLaunchDescriptorResult.Fail(
                NativeAppLaunchDescriptorStatus.AppPathUnavailable,
                NativeAppLaunchStatus.InvalidAppPathField,
                OsPackageFileSystemStatus.Success,
                OsPackageStatus.Success);
        }

        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, appPath);
        if (!open.Succeeded)
        {
            return NativeAppLaunchDescriptorResult.Fail(
                MapOpenStatus(open.Status),
                NativeAppLaunchStatus.Success,
                open.Status,
                open.PackageStatus);
        }

        ReadOnlySpan<byte> appPayload = package.Slice(
            (int)open.Entry.ContentOffset,
            (int)open.Entry.ContentLength);
        NativeAppPayloadHeaderResult payloadHeader = NativeAppPayloadHeader.Parse(appPayload);
        if (!payloadHeader.Succeeded)
        {
            return NativeAppLaunchDescriptorResult.Fail(
                NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
                NativeAppLaunchStatus.Success,
                OsPackageFileSystemStatus.Success,
                OsPackageStatus.Success,
                payloadHeader.Status);
        }

        NativeAppLaunchDescriptor descriptor = new NativeAppLaunchDescriptor(
            launchPlan.Plan,
            open.Entry,
            open.FileLength,
            launchPlan.Plan.RequestedNetworkMode,
            launchPlan.Plan.LogMode,
            launchPlan.Plan.ArgumentByteLength,
            payloadHeader.Header);
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
