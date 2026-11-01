namespace OS.Manifest;
public readonly struct NativeManifest
{
    public const byte Version0 = 0;
    public NativeManifest(byte version, NativeManifestField appName, NativeManifestField appPath, NativeManifestField args, NativeManifestAppKind appKind, NativeManifestRuntimeKind runtimeKind, NativeManifestNetworkMode networkMode, NativeManifestLogMode logMode) : this(version, appName, appPath, args, appKind, runtimeKind, networkMode, logMode, default, default)
    {
    }

    public NativeManifest(byte version, NativeManifestField appName, NativeManifestField appPath, NativeManifestField args, NativeManifestAppKind appKind, NativeManifestRuntimeKind runtimeKind, NativeManifestNetworkMode networkMode, NativeManifestLogMode logMode, NativeManifestEnvironmentBlock environment) : this(version, appName, appPath, args, appKind, runtimeKind, networkMode, logMode, environment, default)
    {
    }

    public NativeManifest(byte version, NativeManifestField appName, NativeManifestField appPath, NativeManifestField args, NativeManifestAppKind appKind, NativeManifestRuntimeKind runtimeKind, NativeManifestNetworkMode networkMode, NativeManifestLogMode logMode, NativeManifestEnvironmentBlock environment, NativeManifestListenEndpoint listenEndpoint)
    {
        Version = version;
        AppName = appName;
        AppPath = appPath;
        Args = args;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        NetworkMode = networkMode;
        LogMode = logMode;
        Environment = environment;
        ListenEndpoint = listenEndpoint;
    }

    public byte Version { get; }
    public NativeManifestField AppName { get; }
    public NativeManifestField AppPath { get; }
    public NativeManifestField Args { get; }
    public NativeManifestAppKind AppKind { get; }
    public NativeManifestRuntimeKind RuntimeKind { get; }
    public NativeManifestNetworkMode NetworkMode { get; }
    public NativeManifestLogMode LogMode { get; }
    public NativeManifestEnvironmentBlock Environment { get; }
    public int EnvironmentCount => Environment.Count;
    public NativeManifestListenEndpoint ListenEndpoint { get; }
    public bool HasListenEndpoint => ListenEndpoint.IsPresent;

    public NativeManifestEnvironmentEntry GetEnvironmentEntry(int index)
    {
        return Environment.GetAt(index);
    }
}
