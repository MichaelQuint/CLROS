namespace OS.Tooling;

public sealed class EfiOptions
{
    private readonly Dictionary<string, List<string>> values = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Probes { get; } = new(StringComparer.Ordinal);
    public string Root { get; private set; } = Repository.FindRoot();
    /// <summary>Resolve staged-package children and diagnostic ancestors while retaining explicit-false prerequisite rejection.</summary>
    public EfiProfile Profile(Action<string>? emit = null) => EfiProfile.Create(Get("BootPolicy", ""), Probes, emit);
    private static string Key(string name) => name.TrimStart('-').Replace("-", "", StringComparison.Ordinal);
    public string Get(string name, string fallback = "") => values.TryGetValue(Key(name), out var found) ? found[^1] : fallback;
    public IReadOnlyList<string> All(string name) => values.TryGetValue(Key(name), out var found) ? found : [];
    public bool Flag(string name) => Get(name, "false").Equals("true", StringComparison.OrdinalIgnoreCase);
    public int Number(string name, int fallback, int minimum, int maximum)
    {
        if (!int.TryParse(Get(name, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)), out int number) || number < minimum || number > maximum) throw new ToolFailure("argument_out_of_range", $"property={name} minimum={minimum} maximum={maximum}");
        return number;
    }
    public static EfiOptions Parse(string[] args, bool build)
    {
        EfiOptions result = new();
        HashSet<string> allowed = new((build
            ? new[] { "Root", "BootPolicy", "Configuration", "Runtime", "LogPath", "EfiOutputPath", "VsWherePath", "MsvcLinkerSearchRoot", "WindowsSdkLibSearchRoot", "IlCompilerPath", "IlCompilerSearchRoot", "NativeAotRuntimePackPath", "NativeAotRuntimePackSearchRoot", "QemuPath", "QemuSearchRoot", "QemuImgPath", "QemuImgSearchRoot", "OvmfCodePath", "OvmfSearchRoot", "VersionProbeTimeoutMilliseconds", "ArtifactVerifierTimeoutSeconds", "BuildProcessTimeoutSeconds", "BuildProcessMaximumOutputCharacters", "PreflightOnly" }
            : new[] { "Root", "BootPolicy", "EfiArtifact", "ProjectPath", "EntrySourcePath", "LinkerMapPath", "LinkResponsePath", "LinkEvidencePath", "SerializerObjectPath" }).Select(Key), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> probeKeys = EfiContracts.DiagnosticProbes.ToDictionary(Key, p => p, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith('-')) throw new ToolFailure("argument_invalid", arg);
            int separator = arg.IndexOfAny([':', '=']);
            string name = Key(separator < 0 ? arg : arg[..separator]);
            string? value = separator < 0 ? null : arg[(separator + 1)..];
            bool probe = probeKeys.TryGetValue(name, out string? canonical);
            if (!probe && !allowed.Contains(name)) throw new ToolFailure("argument_unknown", arg);
            bool boolean = probe || name.Equals("PreflightOnly", StringComparison.OrdinalIgnoreCase);
            if (value is null && (!boolean || i + 1 < args.Length && bool.TryParse(args[i + 1], out _)))
            {
                if (++i >= args.Length) throw new ToolFailure("argument_value_missing", arg);
                value = args[i];
            }
            value ??= "true";
            if (boolean)
            {
                if (!bool.TryParse(value.TrimStart('$'), out bool flag)) throw new ToolFailure("argument_boolean_invalid", arg);
                value = flag ? "true" : "false";
                if (probe && !result.Probes.TryAdd(canonical!, flag)) throw new ToolFailure("argument_duplicate", arg);
            }
            if (!result.values.TryGetValue(name, out var entries)) result.values[name] = entries = [];
            else if (!name.EndsWith("SearchRoot", StringComparison.OrdinalIgnoreCase)) throw new ToolFailure("argument_duplicate", arg);
            entries.Add(value);
        }
        result.Root = Path.GetFullPath(result.Get("Root", result.Root));
        return result;
    }
}
