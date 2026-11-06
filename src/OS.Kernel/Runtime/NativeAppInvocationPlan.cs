using OS.Boot;
using OS.Manifest;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppInvocationPlan
{
    public NativeAppInvocationPlan(ulong requiredStackBytes, ulong requiredWritableStagingBytes, ulong executableMapOffsetInPackage, ulong executableMapByteLength, ulong executableMapEndOffsetInPackage, ulong executableContentOffsetInMapping, ulong executableBytes, ulong entryOffsetInExecutableContent, ulong entryOffsetInExecutableMapping, ulong entryOffsetInPackage, int manifestMetadataByteCount, byte manifestVersion, NativeManifestAppKind appKind, NativeManifestRuntimeKind runtimeKind, NativeManifestNetworkMode networkMode, NativeManifestLogMode logMode, int argumentByteLength, int environmentCount, BootCapabilities requiredBootCapabilities, NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities)
    {
        RequiredStackBytes = requiredStackBytes;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        ExecutableMapOffsetInPackage = executableMapOffsetInPackage;
        ExecutableMapByteLength = executableMapByteLength;
        ExecutableMapEndOffsetInPackage = executableMapEndOffsetInPackage;
        ExecutableContentOffsetInMapping = executableContentOffsetInMapping;
        ExecutableBytes = executableBytes;
        EntryOffsetInExecutableContent = entryOffsetInExecutableContent;
        EntryOffsetInExecutableMapping = entryOffsetInExecutableMapping;
        EntryOffsetInPackage = entryOffsetInPackage;
        ManifestMetadataByteCount = manifestMetadataByteCount;
        ManifestVersion = manifestVersion;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        NetworkMode = networkMode;
        LogMode = logMode;
        ArgumentByteLength = argumentByteLength;
        EnvironmentCount = environmentCount;
        RequiredBootCapabilities = requiredBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
    }

    public ulong RequiredStackBytes
    {
        get;
    }
    public ulong RequiredWritableStagingBytes
    {
        get;
    }
    public ulong ExecutableMapOffsetInPackage
    {
        get;
    }
    public ulong ExecutableMapByteLength
    {
        get;
    }
    public ulong ExecutableMapEndOffsetInPackage
    {
        get;
    }
    public ulong ExecutableContentOffsetInMapping
    {
        get;
    }
    public ulong ExecutableBytes
    {
        get;
    }
    public ulong EntryOffsetInExecutableContent
    {
        get;
    }
    public ulong EntryOffsetInExecutableMapping
    {
        get;
    }
    public ulong EntryOffsetInPackage
    {
        get;
    }
    public int ManifestMetadataByteCount
    {
        get;
    }
    public byte ManifestVersion
    {
        get;
    }
    public NativeManifestAppKind AppKind
    {
        get;
    }
    public NativeManifestRuntimeKind RuntimeKind
    {
        get;
    }
    public NativeManifestNetworkMode NetworkMode
    {
        get;
    }
    public NativeManifestLogMode LogMode
    {
        get;
    }
    public int ArgumentByteLength
    {
        get;
    }
    public int EnvironmentCount
    {
        get;
    }
    public BootCapabilities RequiredBootCapabilities
    {
        get;
    }
    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities
    {
        get;
    }
}
