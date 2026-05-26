using OS.Boot;

namespace OS.Manifest;

[Flags]
public enum NativeAppLaunchRuntimeCapabilities
{
    None = 0,
    PackageStorage = 1,
    Network = 2
}

public enum NativeAppLaunchStatus
{
    Success = 0,
    UnsupportedManifestVersion,
    MissingAppName,
    MissingAppPath,
    MissingArguments,
    InvalidAppNameField,
    InvalidAppPathField,
    InvalidArgumentsField,
    AppPathNotPackageRelative,
    ArgumentsTooLong,
    UnsupportedAppKind,
    UnsupportedRuntimeKind,
    UnsupportedNetworkMode,
    UnsupportedLogMode,
    InvalidListenEndpoint,
    ListenRequiresNetwork,
    MissingBootCapabilities,
    UnknownBootCapabilities,
    MissingRuntimeCapabilities
}

public enum NativeAppStorageRequirement
{
    None = 0,
    PackageFile = 1
}

public readonly struct NativeAppLaunchPlanResult
{
    private NativeAppLaunchPlanResult(
        NativeAppLaunchStatus status,
        bool isInitialized,
        NativeAppLaunchPlan plan,
        BootCapabilities requiredBootCapabilities,
        BootCapabilities missingBootCapabilities,
        BootCapabilities unknownBootCapabilities,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities,
        NativeAppLaunchRuntimeCapabilities missingRuntimeCapabilities)
    {
        Status = status;
        IsInitialized = isInitialized;
        Plan = plan;
        RequiredBootCapabilities = requiredBootCapabilities;
        MissingBootCapabilities = missingBootCapabilities;
        UnknownBootCapabilities = unknownBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
        MissingRuntimeCapabilities = missingRuntimeCapabilities;
    }

    public NativeAppLaunchStatus Status { get; }

    public bool IsInitialized { get; }

    public NativeAppLaunchPlan Plan { get; }

    public BootCapabilities RequiredBootCapabilities { get; }

    public BootCapabilities MissingBootCapabilities { get; }

    public BootCapabilities UnknownBootCapabilities { get; }

    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities { get; }

    public NativeAppLaunchRuntimeCapabilities MissingRuntimeCapabilities { get; }

    public bool Succeeded => IsInitialized && Status == NativeAppLaunchStatus.Success;

    public static NativeAppLaunchPlanResult Success(
        NativeAppLaunchPlan plan,
        BootCapabilities requiredBootCapabilities,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities)
    {
        return new NativeAppLaunchPlanResult(
            NativeAppLaunchStatus.Success,
            isInitialized: true,
            plan,
            requiredBootCapabilities,
            BootCapabilities.None,
            BootCapabilities.None,
            requiredRuntimeCapabilities,
            NativeAppLaunchRuntimeCapabilities.None);
    }

    public static NativeAppLaunchPlanResult Fail(
        NativeAppLaunchStatus status,
        BootCapabilities requiredBootCapabilities,
        BootCapabilities missingBootCapabilities,
        BootCapabilities unknownBootCapabilities,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities,
        NativeAppLaunchRuntimeCapabilities missingRuntimeCapabilities)
    {
        return new NativeAppLaunchPlanResult(
            status,
            isInitialized: true,
            default,
            requiredBootCapabilities,
            missingBootCapabilities,
            unknownBootCapabilities,
            requiredRuntimeCapabilities,
            missingRuntimeCapabilities);
    }
}

public readonly struct NativeAppLaunchPlan
{
    private NativeAppLaunchPlan(
        byte manifestVersion,
        NativeManifestField appName,
        NativeManifestField appPath,
        NativeManifestField arguments,
        int argumentByteLength,
        NativeManifestAppKind appKind,
        NativeManifestRuntimeKind runtimeKind,
        NativeManifestNetworkMode requestedNetworkMode,
        NativeManifestLogMode logMode,
        NativeManifestListenEndpoint listenEndpoint,
        NativeManifestEnvironmentBlock environment,
        NativeAppStorageRequirement storageRequirement,
        BootCapabilities requiredBootCapabilities,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities)
    {
        ManifestVersion = manifestVersion;
        AppName = appName;
        AppPath = appPath;
        Arguments = arguments;
        ArgumentByteLength = argumentByteLength;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        RequestedNetworkMode = requestedNetworkMode;
        LogMode = logMode;
        ListenEndpoint = listenEndpoint;
        Environment = environment;
        StorageRequirement = storageRequirement;
        RequiredBootCapabilities = requiredBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
    }

    public byte ManifestVersion { get; }

    public NativeManifestField AppName { get; }

    public NativeManifestField AppPath { get; }

    public NativeManifestField Arguments { get; }

    public int ArgumentByteLength { get; }

    public NativeManifestEnvironmentBlock Environment { get; }

    public int EnvironmentCount => Environment.Count;

    public NativeManifestAppKind AppKind { get; }

    public NativeManifestRuntimeKind RuntimeKind { get; }

    public NativeManifestNetworkMode RequestedNetworkMode { get; }

    public NativeManifestLogMode LogMode { get; }

    public NativeManifestListenEndpoint ListenEndpoint { get; }

    public bool HasListenEndpoint => ListenEndpoint.IsPresent;

    public NativeAppStorageRequirement StorageRequirement { get; }

    public bool RequiresPackageStorage => StorageRequirement == NativeAppStorageRequirement.PackageFile;

    public bool RequiresNetwork =>
        RequestedNetworkMode != NativeManifestNetworkMode.None || ListenEndpoint.IsPresent;

    public ushort ListenPort => ListenEndpoint.Port;

    public BootCapabilities RequiredBootCapabilities { get; }

    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities { get; }

    public bool TryGetAppName(ReadOnlySpan<byte> manifestSource, out ReadOnlySpan<byte> value)
    {
        return AppName.TryGetSlice(manifestSource, out value);
    }

    public bool TryGetAppPath(ReadOnlySpan<byte> manifestSource, out ReadOnlySpan<byte> value)
    {
        return AppPath.TryGetSlice(manifestSource, out value);
    }

    public bool TryGetArguments(ReadOnlySpan<byte> manifestSource, out ReadOnlySpan<byte> value)
    {
        return Arguments.TryGetSlice(manifestSource, out value);
    }

    public NativeManifestEnvironmentEntry GetEnvironmentEntry(int index)
    {
        return Environment.GetAt(index);
    }

    public bool TryGetListenHost(ReadOnlySpan<byte> manifestSource, out ReadOnlySpan<byte> value)
    {
        return ListenEndpoint.TryGetHost(manifestSource, out value);
    }

    public static NativeAppLaunchPlanResult Create(
        ReadOnlySpan<byte> manifestSource,
        NativeManifest manifest,
        BootInfo bootInfo,
        NativeAppLaunchRuntimeCapabilities availableRuntimeCapabilities)
    {
        if (manifest.Version != NativeManifest.Version0)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.UnsupportedManifestVersion);
        }

        if (!manifest.AppName.IsPresent)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.MissingAppName);
        }

        if (!manifest.AppName.TryGetSlice(manifestSource, out ReadOnlySpan<byte> appName))
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.InvalidAppNameField);
        }

        if (appName.Length == 0)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.MissingAppName);
        }

        if (!manifest.AppPath.IsPresent)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.MissingAppPath);
        }

        if (!manifest.AppPath.TryGetSlice(manifestSource, out ReadOnlySpan<byte> appPath))
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.InvalidAppPathField);
        }

        if (appPath.Length == 0)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.MissingAppPath);
        }

        if (!IsPackageRelativePath(appPath))
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.AppPathNotPackageRelative);
        }

        if (!manifest.Args.IsPresent)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.MissingArguments);
        }

        if (!manifest.Args.TryGetSlice(manifestSource, out ReadOnlySpan<byte> arguments))
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.InvalidArgumentsField);
        }

        if (arguments.Length > NativeManifestParser.MaximumArgsLength)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.ArgumentsTooLong);
        }

        if (manifest.AppKind != NativeManifestAppKind.Package)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.UnsupportedAppKind);
        }

        if (manifest.RuntimeKind != NativeManifestRuntimeKind.NativeAot)
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.UnsupportedRuntimeKind);
        }

        if (!IsSupportedNetworkMode(manifest.NetworkMode))
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.UnsupportedNetworkMode);
        }

        if (!IsSupportedLogMode(manifest.LogMode))
        {
            return FailWithoutRequirements(NativeAppLaunchStatus.UnsupportedLogMode);
        }

        if (manifest.ListenEndpoint.IsPresent)
        {
            if (!manifest.ListenEndpoint.TryGetHost(manifestSource, out ReadOnlySpan<byte> listenHost)
                || !IsValidListenHost(listenHost)
                || manifest.ListenEndpoint.Port == 0)
            {
                return FailWithoutRequirements(NativeAppLaunchStatus.InvalidListenEndpoint);
            }

            if (manifest.NetworkMode == NativeManifestNetworkMode.None)
            {
                return FailWithoutRequirements(NativeAppLaunchStatus.ListenRequiresNetwork);
            }
        }

        BootCapabilities requiredBootCapabilities = RequiredBootCapabilitiesFor(manifest);
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities = RequiredRuntimeCapabilitiesFor(manifest);

        BootCapabilityResult bootCapabilityResult = bootInfo.RequireCapability(requiredBootCapabilities);
        if (!bootCapabilityResult.Succeeded)
        {
            NativeAppLaunchStatus status = bootCapabilityResult.UnknownCapabilities == BootCapabilities.None
                ? NativeAppLaunchStatus.MissingBootCapabilities
                : NativeAppLaunchStatus.UnknownBootCapabilities;

            return NativeAppLaunchPlanResult.Fail(
                status,
                requiredBootCapabilities,
                bootCapabilityResult.MissingCapabilities,
                bootCapabilityResult.UnknownCapabilities,
                requiredRuntimeCapabilities,
                NativeAppLaunchRuntimeCapabilities.None);
        }

        NativeAppLaunchRuntimeCapabilities missingRuntimeCapabilities =
            requiredRuntimeCapabilities & ~availableRuntimeCapabilities;
        if (missingRuntimeCapabilities != NativeAppLaunchRuntimeCapabilities.None)
        {
            return NativeAppLaunchPlanResult.Fail(
                NativeAppLaunchStatus.MissingRuntimeCapabilities,
                requiredBootCapabilities,
                BootCapabilities.None,
                BootCapabilities.None,
                requiredRuntimeCapabilities,
                missingRuntimeCapabilities);
        }

        NativeAppLaunchPlan plan = new NativeAppLaunchPlan(
            manifest.Version,
            manifest.AppName,
            manifest.AppPath,
            manifest.Args,
            arguments.Length,
            manifest.AppKind,
            manifest.RuntimeKind,
            manifest.NetworkMode,
            manifest.LogMode,
            manifest.ListenEndpoint,
            manifest.Environment,
            NativeAppStorageRequirement.PackageFile,
            requiredBootCapabilities,
            requiredRuntimeCapabilities);

        return NativeAppLaunchPlanResult.Success(
            plan,
            requiredBootCapabilities,
            requiredRuntimeCapabilities);
    }

    private static NativeAppLaunchPlanResult FailWithoutRequirements(NativeAppLaunchStatus status)
    {
        return NativeAppLaunchPlanResult.Fail(
            status,
            BootCapabilities.None,
            BootCapabilities.None,
            BootCapabilities.None,
            NativeAppLaunchRuntimeCapabilities.None,
            NativeAppLaunchRuntimeCapabilities.None);
    }

    private static BootCapabilities RequiredBootCapabilitiesFor(NativeManifest manifest)
    {
        BootCapabilities capabilities = BootCapabilities.None;
        if (manifest.LogMode == NativeManifestLogMode.Serial)
        {
            capabilities |= BootCapabilities.TextOutput;
        }

        return capabilities;
    }

    private static NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilitiesFor(NativeManifest manifest)
    {
        NativeAppLaunchRuntimeCapabilities capabilities = NativeAppLaunchRuntimeCapabilities.PackageStorage;
        if (manifest.NetworkMode != NativeManifestNetworkMode.None || manifest.ListenEndpoint.IsPresent)
        {
            capabilities |= NativeAppLaunchRuntimeCapabilities.Network;
        }

        return capabilities;
    }

    private static bool IsSupportedNetworkMode(NativeManifestNetworkMode networkMode)
    {
        return networkMode == NativeManifestNetworkMode.None
            || networkMode == NativeManifestNetworkMode.Dhcp;
    }

    private static bool IsSupportedLogMode(NativeManifestLogMode logMode)
    {
        return logMode == NativeManifestLogMode.None
            || logMode == NativeManifestLogMode.Serial;
    }

    private static bool IsPackageRelativePath(ReadOnlySpan<byte> path)
    {
        if (path.Length == 0 || path[0] == (byte)'/' || path[0] == (byte)'\\')
        {
            return false;
        }

        int segmentStart = 0;
        for (int index = 0; index <= path.Length; index++)
        {
            if (index == path.Length || path[index] == (byte)'/')
            {
                int segmentLength = index - segmentStart;
                if (segmentLength == 0 || IsDotSegment(path.Slice(segmentStart, segmentLength)))
                {
                    return false;
                }

                segmentStart = index + 1;
                continue;
            }

            byte value = path[index];
            if (value == (byte)'\\' || value == (byte)':')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDotSegment(ReadOnlySpan<byte> segment)
    {
        return OS.Std.MemoryExtensions.SequenceEqual(segment, "."u8)
            || OS.Std.MemoryExtensions.SequenceEqual(segment, ".."u8);
    }

    private static bool IsValidListenHost(ReadOnlySpan<byte> host)
    {
        if (host.Length == 0 || host.Length > NativeManifestParser.MaximumListenHostLength)
        {
            return false;
        }

        bool previousWasDot = false;
        for (int index = 0; index < host.Length; index++)
        {
            byte value = host[index];
            if (value == (byte)'.')
            {
                if (index == 0 || index == host.Length - 1 || previousWasDot)
                {
                    return false;
                }

                previousWasDot = true;
                continue;
            }

            previousWasDot = false;
            if (value == (byte)'-')
            {
                if (index == 0 || index == host.Length - 1)
                {
                    return false;
                }

                continue;
            }

            if (!OS.Std.Ascii.IsLetterOrDigit(value))
            {
                return false;
            }
        }

        return true;
    }
}
