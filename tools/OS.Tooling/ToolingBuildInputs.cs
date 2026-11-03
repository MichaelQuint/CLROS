using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
/// <summary>Compiled input manifest with repository-relative relocation and inventory checks.</summary>

public static partial class ToolingBuildInputs
{
    internal const string ReparsePointDiagnosticPrefix = "tooling_build_input_reparse_point ";
    private const string ResourceName = "tooling-build-inputs.txt", Format = "CLROS_BUILD_INPUTS_V1";
    private static readonly Lazy<Manifest> Compiled = new(() =>
    {
        using Stream stream = typeof(ToolingBuildInputs).Assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException("tooling_build_input_manifest_missing rebuild_required");
        using StreamReader reader = new(stream);
        return Read(reader);
    });
    public static IReadOnlyList<string> Validate(string root) => Validate(Compiled.Value, root);
    public static IReadOnlyList<string> Validate(TextReader reader, string root) => Validate(Read(reader), root);
    public static IReadOnlyList<string> SourceInputs(string root) => Expand(root, Compiled.Value.Patterns);
    public static bool RequireCurrent(string[] args, TextWriter error)
    {
        try
        {
            string? root = null;
            if (args.Length >= 2 && args[0] == "efi")
                for (int i = 2; i < args.Length; i++)
                {
                    string token = args[i];
                    int at = token.IndexOfAny(['=', ':']);
                    string name = (at < 0 ? token : token[..at]).TrimStart('-').Replace("-", "", StringComparison.Ordinal);
                    if (!name.Equals("root", StringComparison.OrdinalIgnoreCase))
                        continue;
                    root = at >= 0 ? token[(at + 1)..] : ++i < args.Length ? args[i] : throw new ArgumentException("root_value_missing");
                }

            var failures = Validate(Path.GetFullPath(root ?? Repository.FindRoot()));
            foreach (string failure in failures.Take(10))
                error.WriteLine("TEST_FAIL tooling.inputs " + failure + " rebuild_required");
            if (failures.Count > 10)
                error.WriteLine("TEST_FAIL tooling.inputs additional_failures=" + (failures.Count - 10) + " rebuild_required");
            return failures.Count == 0;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            error.WriteLine("TEST_FAIL tooling.inputs " + failure.Message.Replace('\r', ' ').Replace('\n', ' ') + " rebuild_required");
            return false;
        }
    }

    private static Manifest Read(TextReader reader)
    {
        if (reader.ReadLine() != Format)
            throw new InvalidOperationException("tooling_build_input_manifest_invalid");
        string rootLine = reader.ReadLine() ?? "";
        if (!rootLine.StartsWith("ROOT|", StringComparison.Ordinal))
            throw new InvalidOperationException("tooling_build_input_manifest_root_missing");
        string originalRoot = CapturedPath(rootLine[5..]).TrimEnd('/') + "/";
        StringComparison comparison = originalRoot[0] == '/' ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        List<string> patterns = [];
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;
            if (line.StartsWith("GLOB|", StringComparison.Ordinal))
            {
                string pattern = line[5..].Replace('\\', '/');
                if (pattern.Length == 0 || pattern.Any(char.IsControl) || pattern.IndexOfAny([':', '|']) >= 0 || Path.IsPathRooted(pattern) || pattern.Split('/').Any(p => p is ".." or "." or ""))
                    throw new InvalidOperationException("tooling_build_input_pattern_invalid");
                patterns.Add(pattern);
                continue;
            }

            if (!line.StartsWith("FILE|", StringComparison.Ordinal))
                throw new InvalidOperationException("tooling_build_input_manifest_record_invalid");
            int separator = line.LastIndexOf('|');
            if (separator <= 5)
                throw new InvalidOperationException("tooling_build_input_manifest_record_invalid");
            string absolute = CapturedPath(line[5..separator]), hash = line[(separator + 1)..];
            if (!absolute.StartsWith(originalRoot, comparison) || absolute.Length == originalRoot.Length || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new InvalidOperationException("tooling_build_input_manifest_identity_invalid");
            string relative = absolute[originalRoot.Length..];
            if (!files.TryAdd(relative, hash) && files[relative] != hash)
                throw new InvalidOperationException("tooling_build_input_manifest_duplicate");
        }

        if (files.Count == 0 || patterns.Count == 0)
            throw new InvalidOperationException("tooling_build_input_manifest_empty");
        return new(patterns.AsReadOnly(), files.AsReadOnly());
    }

    // These paths describe the build host, which may differ from this host.
    // Never resolve them against the destination's current drive/directory.
    // Only their checked relative suffixes are used to open current inputs.
    private static string CapturedPath(string value)
    {
        string path = value.Replace('\\', '/');
        int prefix = path.StartsWith('/') ? 1 : path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/' ? 3 : 0;
        if (prefix == 0 || path.Any(char.IsControl) || path[prefix..].TrimEnd('/').Split('/').Any(part => part is "" or "." or ".." || part.IndexOfAny([':', '|']) >= 0))
            throw new InvalidOperationException("tooling_build_input_manifest_path_invalid");
        return path.TrimEnd('/');
    }

    private static IReadOnlyList<string> Validate(Manifest manifest, string root)
    {
        root = Path.GetFullPath(root);
        StringBuilder current = new();
        foreach (var item in manifest.Files)
            current.Append(Path.Combine(root, item.Key)).Append('|').Append(item.Value).AppendLine();
        return CompiledInputValidation.Validate(new StringReader(current.ToString()), Expand(root, manifest.Patterns));
    }

    private static IReadOnlyList<string> Expand(string root, IReadOnlyList<string> patterns)
    {
        root = Path.GetFullPath(root);
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase), directories = new(StringComparer.OrdinalIgnoreCase);
        List<Regex> selectors = [];
        foreach (string pattern in patterns)
        {
            int wildcard = pattern.IndexOfAny(['*', '?']);
            if (wildcard < 0)
            {
                string file = Path.Combine(root, pattern);
                if (File.Exists(file))
                    files.Add(file);
                continue;
            }

            int slash = pattern.LastIndexOf('/', wildcard);
            directories.Add(slash < 0 ? root : Path.Combine(root, pattern[..slash]));
            selectors.Add(new Regex("\\A" + Regex.Escape(pattern).Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal).Replace(@"\*", "[^/]*", StringComparison.Ordinal).Replace(@"\?", "[^/]", StringComparison.Ordinal) + "\\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase));
        }

        foreach (string directory in directories.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
                continue;
            Stack<string> pending = new();
            pending.Push(directory);
            while (pending.TryPop(out string? current))
                foreach (string path in Directory.EnumerateFileSystemEntries(current))
                {
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.Directory) != 0 && Path.GetFileName(path).ToLowerInvariant() is "bin" or "obj" or "artifacts")
                        continue;
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException(ReparsePointDiagnosticPrefix + path);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(path);
                        continue;
                    }

                    string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                    if (selectors.Any(selector => selector.IsMatch(relative)))
                        files.Add(Path.GetFullPath(path));
                }
        }

        return files.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
