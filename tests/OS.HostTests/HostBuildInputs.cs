internal static class HostBuildInputs
{
    private static readonly HashSet<string> GeneratedDirectories = new(StringComparer.OrdinalIgnoreCase) { "obj", "bin", "artifacts" };
    internal static IReadOnlyList<string> Validate()
    {
        using Stream stream = typeof(HostBuildInputs).Assembly.GetManifestResourceStream("host-test-inputs.txt")
            ?? throw new InvalidOperationException("build_input_manifest_missing rebuild_required");
        using StreamReader reader = new(stream);
        return Validate(reader, SourceInputs(HostToolProcess.FindRepositoryRoot()));
    }

    internal static IReadOnlyList<string> Validate(TextReader reader, IEnumerable<string>? currentInputs = null)
        => OS.Tooling.CompiledInputValidation.Validate(reader, currentInputs);

    internal static IEnumerable<string> SourceInputs(string root)
    {
        foreach (string directory in new[] { "src", "std", "tools", "tests" })
            foreach (string path in Walk(Path.Combine(root, directory)))
                yield return path;
        foreach (string path in Walk(Path.Combine(root, "apps"), json: true))
            yield return path;
        yield return Path.Combine(root, "CLROS.slnx");
        yield return Path.Combine(root, ".gitea", "backlog", "clros-issues.json");
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "scripts"), "*.ps1"))
            yield return path;
        string workflows = Path.Combine(root, ".gitea", "workflows");
        if (Directory.Exists(workflows))
            foreach (string path in Directory.EnumerateFiles(workflows, "*.yml"))
                yield return path;
    }

    private static IEnumerable<string> Walk(string directory, bool json = false)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0 && GeneratedDirectories.Contains(Path.GetFileName(path)))
                continue;
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("build_input_reparse_point " + path);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (string child in Walk(path, json))
                    yield return child;
            }
            else if (Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".csproj" or ".props" or ".targets" || json && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                yield return path;
        }
    }
}
