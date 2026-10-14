namespace OS.Tooling;

public sealed record EfiBuildCommands(string Executable, string WorkingDirectory,
    IReadOnlyList<string> Restore, IReadOnlyList<string> Publish,
    IReadOnlyDictionary<string, string?> Environment, int TimeoutSeconds, int MaximumOutputCharacters)
{
    public const bool TerminateDescendantsAfterRootExit = true;
    public static IReadOnlyDictionary<string, string?> ProcessEnvironment { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        { ["MSBUILDDISABLENODEREUSE"] = "1", ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0" }.AsReadOnly();
    public IReadOnlyList<string> ProbeMarkers { get; init; } = [];
    public static EfiBuildCommands Create(EfiOptions options, EfiPreflightResult preflight)
    {
        string root = options.Root, configuration = preflight.Configuration;
        string project = Path.GetFullPath(Path.Combine(root, "src/OS/OS.csproj"));
        string intermediate = Path.GetFullPath(Path.Combine(root, "src/OS/obj", configuration, "net11.0/win-x64"));
        string extensions = Path.Combine(intermediate, "nuget"), packages = Path.Combine(intermediate, "packages");
        var isolation = EfiMsbuild.IsolationProperties(root, extensions, packages, preflight.DotnetSdkDirectory);
        var canonical = EfiMsbuild.CanonicalPaths(packages, preflight.NativeAotRuntimePackPath, preflight.DotnetSdkDirectory);
        Dictionary<string, string?> environment = new(ProcessEnvironment, StringComparer.OrdinalIgnoreCase)
        {
            ["LINK"] = null, ["_LINK_"] = null,
            ["MSBuildSDKsPath"] = isolation["MSBuildSDKsPath"], ["MSBuildExtensionsPath"] = isolation["MSBuildExtensionsPath"],
            ["MSBuildUserExtensionsPath"] = isolation["MSBuildUserExtensionsPath"]
        };
        List<string> common = ["-p:ClrosBootPolicy=" + preflight.Profile.BootPolicy, "-p:RestoreOutputPath=" + extensions.Replace('\\', '/') + "/"];
        common.AddRange(isolation.Select(pair => "-p:" + pair.Key + "=" + pair.Value));
        List<string> restore = ["restore", project, "-noAutoResponse", "-nodeReuse:false", "-r", "win-x64", "--nologo", "--force", "--no-cache", "--configfile", Path.Combine(intermediate, "restore-source/NuGet.Config"), "--packages", packages,
            "-p:Configuration=" + configuration, "-p:RestoreForceEvaluate=true", "-p:RestoreUseCustomAfterTargets=false", "-p:RestoreIgnoreFailedSources=false"];
        restore.AddRange(common);
        List<string> publish = ["publish", project, "-noAutoResponse", "-nodeReuse:false", "-c", configuration, "-r", "win-x64", "--nologo", "--no-restore",
            "-p:CppLinker=" + preflight.LinkerPath, "-p:IlcGenerateMapFile=true", "-p:NoIncremental=true"];
        publish.AddRange(common); publish.AddRange(EfiMsbuild.NativeProperties(canonical).Select(pair => "-p:" + pair.Key + "=" + pair.Value));
        publish.AddRange(preflight.Profile.Flags.Select(pair => "-p:" + pair.Key + "=" + (pair.Value ? "true" : "false")));
        return new(preflight.DotnetHostPath, root, restore.AsReadOnly(), publish.AsReadOnly(), environment.AsReadOnly(), EfiBuildPolicy.BuildTimeout(options), EfiBuildPolicy.OutputLimit(options))
        {
            ProbeMarkers = System.Array.AsReadOnly(preflight.Profile.Flags.Where(pair => pair.Value).Select(pair => "EFI_PROBE " + EfiContracts.ProbeSymbols[pair.Key].Replace("_PROBE", "", StringComparison.Ordinal)).ToArray())
        };
    }
}
