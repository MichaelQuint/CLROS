using OS.Boot;
using OS.Manifest;
using OS.Pal;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppLifecyclePlan
{
    public NativeAppLifecyclePlan(byte manifestVersion, NativeManifestAppKind appKind, NativeManifestRuntimeKind runtimeKind, NativeManifestNetworkMode networkMode, NativeManifestLogMode logMode, int argumentByteLength, int environmentCount, ulong requiredStackBytes, ulong requiredWritableStagingBytes, BootCapabilities requiredBootCapabilities, NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities, int exitMarkerMaximumBytes, int logRecordMaximumBytes, int logRemainingRecordCapacity)
    {
        ManifestVersion = manifestVersion;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        NetworkMode = networkMode;
        LogMode = logMode;
        ArgumentByteLength = argumentByteLength;
        EnvironmentCount = environmentCount;
        RequiredStackBytes = requiredStackBytes;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        RequiredBootCapabilities = requiredBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
        ExitMarkerMaximumBytes = exitMarkerMaximumBytes;
        LogRecordMaximumBytes = logRecordMaximumBytes;
        LogRemainingRecordCapacity = logRemainingRecordCapacity;
    }

    public byte ManifestVersion { get; }
    public NativeManifestAppKind AppKind { get; }
    public NativeManifestRuntimeKind RuntimeKind { get; }
    public NativeManifestNetworkMode NetworkMode { get; }
    public NativeManifestLogMode LogMode { get; }
    public int ArgumentByteLength { get; }
    public int EnvironmentCount { get; }
    public ulong RequiredStackBytes { get; }
    public ulong RequiredWritableStagingBytes { get; }
    public BootCapabilities RequiredBootCapabilities { get; }
    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities { get; }
    public int ExitMarkerMaximumBytes { get; }
    public int LogRecordMaximumBytes { get; }
    public int LogRemainingRecordCapacity { get; }
}
