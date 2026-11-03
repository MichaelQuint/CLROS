using System.Text.RegularExpressions;

namespace OS.Tooling;

public static class RepositoryChecks
{
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        { ".claude", ".git", ".vs", "artifacts", "bin", "obj", "references" };
    private static readonly HashSet<string> SourceRoots = new(StringComparer.OrdinalIgnoreCase) { "src", "std", "apps" };
    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase) { ".c", ".cpp", ".h", ".asm", ".s" };
    private static readonly HashSet<string> BuildInputs = new(StringComparer.OrdinalIgnoreCase) { ".csproj", ".vbproj", ".fsproj", ".props", ".targets" };
    private static readonly (Regex Pattern, string Reason, bool AllowReference)[] Naming =
    [
        (Pattern("SHARPOS_BOOT_OK"), "Use OS_BOOT_OK for first-party smoke markers.", false),
        (Pattern(@"sharpos\.raw"), "Use artifacts/images/os.raw for first-party image defaults.", false),
        (Pattern(@"SharpOS\s+(project|workspace|core|kernel|release|completion|gate|gates)"), "Use CLROS for first-party project naming; SharpOS is a reference only.", true),
        (Pattern(@"first-party\s+SharpOS"), "Use first-party CLROS; SharpOS is a reference only.", false),
        (Pattern(@"`src/Boot/BootSequence\.cs`"), "Use src/OS.Kernel/Boot/BootSequence.cs.", false),
        (Pattern(@"`src/Kernel/Diagnostics/Probes\.cs`"), "Use src/OS.Kernel/Diagnostics/Probes.cs.", false)
    ];
    private static readonly Regex ReferenceDiscussion = Pattern(@"(\breference(d|s)?\b|\bsnapshots?\b|\bupstream\b|\bprovenance\b|\blicense\b|SharpOS-(main|master)|references[\\/].*SharpOS)");
    private static readonly Regex ReferencePath = Pattern("(^|[\"'\\s=]|[\\\\/])references([\\\\/]|$)");
    private static Regex Pattern(string value) => new(value, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    public static RepositoryCheckResult Check(string root, Action<string>? directoryVisited = null)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);
        List<string> naming = [], source = [];
        foreach (string file in Enumerate(root, directoryVisited))
        {
            string relative = Path.GetRelativePath(root, file);
            string normalized = relative.Replace('\\', '/');
            // The policy declarations themselves contain the prohibited example patterns.
            bool policySource = normalized is "tools/OS.Tooling/RepositoryChecks.cs";
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (SourceRoots.Contains(normalized.Split('/')[0]) && Forbidden.Contains(extension))
                source.Add($"{relative}: forbidden_source_language extension={extension}");
            bool buildInput = BuildInputs.Contains(extension);
            bool solution = relative == Repository.SolutionName;
            int lineNumber = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNumber++;
                if (!policySource)
                    foreach (var rule in Naming)
                        if (rule.Pattern.IsMatch(line) && !(rule.AllowReference && ReferenceDiscussion.IsMatch(line)))
                            naming.Add($"{relative}:{lineNumber}: {rule.Reason}");
                if ((buildInput || solution) && ReferencePath.IsMatch(line))
                {
                    string reason = solution ? "reference_project_in_solution" : extension is ".props" or ".targets"
                        ? "reference_import_in_first_party_build_input" : "reference_project_in_first_party_project";
                    source.Add($"{relative}:{lineNumber}: {reason}");
                }
            }
        }
        if (!File.Exists(Path.Combine(root, Repository.SolutionName)))
            source.Add(Repository.SolutionName + ": required_solution_missing");
        if (File.Exists(Path.Combine(root, "OS.slnx")))
            source.Add("OS.slnx: legacy_solution_sentinel_present");
        return new(naming, source)
        {
            StyleFailures = RepositoryCodeStyle.Check(root).Select(value => value.ToString()).ToArray()
        };
    }

    public static IEnumerable<string> Enumerate(string root, Action<string>? directoryVisited = null)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            directoryVisited?.Invoke(directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                if (Excluded.Contains(Path.GetFileName(path)))
                    continue;
                FileAttributes attributes = File.GetAttributes(path);
                // Retain the previous provider's visibility and never descend through links.
                if ((attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0)
                    continue;
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(path);
                else
                    yield return path;
            }
        }
    }
}
