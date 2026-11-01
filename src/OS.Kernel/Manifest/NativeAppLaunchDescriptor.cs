using OS.Storage;

namespace OS.Manifest;
public readonly struct NativeAppLaunchDescriptor
{
    public NativeAppLaunchDescriptor(NativeAppLaunchPlan plan, OsPackageFileEntry appBinary, ulong appBinaryLength, NativeManifestNetworkMode networkMode, NativeManifestLogMode logMode, int argumentByteLength, NativeAppPayloadHeader payloadHeader)
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
