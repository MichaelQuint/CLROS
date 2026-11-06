using OS.Tooling;

/// <summary>Per-worker immutable source snapshots shared by fast and native
/// fixtures. The runner's original input guard still validates all source bytes
/// before accepting results; discovery never supplies expected behavior. Fixtures
/// that mutate their own inputs must create a fresh uncached evaluation instead.</summary>
internal static class HostSourceCatalog
{
    private static readonly Dictionary<string, CSharpProjectEvaluation> Evaluations = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, CSharpEfiProjectEvaluation> EfiEvaluations = new(StringComparer.Ordinal);

    internal static CSharpSourceCatalog Efi(EfiProfile profile, string configuration = "Release", string workload = "SingleMinimalApi",
        int queryTimeoutSeconds = 120)
        => EfiEvaluation(profile, configuration, workload, queryTimeoutSeconds).Sources;

    internal static CSharpEfiProjectEvaluation EfiEvaluation(EfiProfile profile, string configuration = "Release", string workload = "SingleMinimalApi",
        int queryTimeoutSeconds = 120)
    {
        string key = EfiCacheKey(profile, configuration, workload, queryTimeoutSeconds);
        lock (EfiEvaluations)
        {
            if (!EfiEvaluations.TryGetValue(key, out var evaluated))
            {
                evaluated = CSharpEfiProjectEvaluation.LoadAsync(HostToolProcess.FindRepositoryRoot(), profile, configuration, workload,
                    queryTimeoutSeconds: queryTimeoutSeconds)
                    .GetAwaiter().GetResult();
                EfiEvaluations.Add(key, evaluated);
            }
            return evaluated;
        }
    }

    internal static string EfiCacheKey(EfiProfile profile, string configuration, string workload, int queryTimeoutSeconds)
    {
        if (queryTimeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(queryTimeoutSeconds));
        return configuration + "|" + workload + "|" + queryTimeoutSeconds + "|" + string.Join(";", profile.CommandArguments());
    }

    internal static CSharpSourceCatalog Project(string project,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var selected = properties is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        string configuration = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>(
            typeof(HostSourceCatalog).Assembly)?.Configuration
            ?? throw new InvalidOperationException("source_catalog_build_configuration_missing");
        selected.TryAdd("Configuration", configuration);
        string key = project + "|" + string.Join(";", selected.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        lock (Evaluations)
        {
            if (!Evaluations.TryGetValue(key, out var evaluated))
            {
                evaluated = CSharpProjectEvaluation.LoadAsync(HostToolProcess.FindRepositoryRoot(), project, selected).GetAwaiter().GetResult();
                Evaluations.Add(key, evaluated);
            }
            return evaluated.Sources;
        }
    }
}
