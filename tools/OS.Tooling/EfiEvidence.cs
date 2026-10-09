using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OS.Tooling;

public sealed record EfiInput(string Path, string Sha256, long Bytes = -1);
public sealed record EfiSnapshot(IReadOnlyList<EfiInput> Inputs, string AggregateSha256, long TotalBytes)
{
    public int FileCount => Inputs.Count;
    public JsonArray ToJson(bool includeBytes = false) => new(Inputs.Select(i => (JsonNode)new JsonObject
    {
        ["path"] = i.Path, ["sha256"] = i.Sha256
    }).Select((n, i) => { if (includeBytes) n["bytes"] = Inputs[i].Bytes; return n; }).ToArray());
}

/// <summary>Exact, locked input snapshots and strict evidence schema readers used by producer and verifier.</summary>
public static class EfiEvidence
{
    public const long MaximumFileBytes = 67108864, MaximumTextBytes = 16777216;
    public static string FullPath(string root, string value, string reason)
    {
        try
        {
            EfiPeImage.Require(!string.IsNullOrWhiteSpace(value) && !value.Contains('\0'), reason);
            return Path.GetFullPath(value, root);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException) { throw new ToolFailure(reason, "type=" + error.GetType().Name); }
    }
    public static string RelativePath(string root, string path, string reason)
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        EfiPeImage.Require(!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal), reason);
        return relative;
    }
    public static string CanonicalRelative(string root, string value, string prefix)
    {
        EfiPeImage.Require(value.Length != 0 && !Path.IsPathRooted(value) && !value.Contains('\\') && !value.Contains(':') && !value.Split('/').Any(s => s is "" or "." or ".."), prefix + "_input_path_invalid", value);
        string full = FullPath(root, value, prefix + "_input_path_invalid");
        EfiPeImage.Require(RelativePath(root, full, prefix + "_input_outside_root") == value, prefix + "_input_path_not_canonical");
        return full;
    }
    public static bool SamePath(string left, string right) => Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    public static string HashLines(IEnumerable<string> lines) => ContractAudit.HashText(string.Concat(lines.Select(s => s + "\n")));
    public static string Aggregate(IEnumerable<EfiInput> inputs, bool lengths = false) => HashLines(inputs.Select(i => i.Path + "|" + (lengths ? i.Bytes.ToString(CultureInfo.InvariantCulture) + "|" : "") + i.Sha256));
    public static bool IsHash(string value) => Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    public static JsonObject Object(JsonNode? value, string reason) => value as JsonObject ?? throw new ToolFailure(reason);
    public static JsonArray Array(JsonNode? value, string reason) => value as JsonArray ?? throw new ToolFailure(reason);
    public static string String(JsonNode? value, string reason)
    {
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out string? text) && text is not null) return text;
        throw new ToolFailure(reason);
    }
    public static long Integer(JsonNode? value, string property)
    {
        if (value is JsonValue scalar && scalar.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out long parsed)) return parsed;
        if (value is JsonValue direct && direct.TryGetValue<long>(out long number)) return number;
        if (value is JsonValue small && small.TryGetValue<int>(out int integer)) return integer;
        throw new ToolFailure("link_evidence_number_malformed", "property=" + property);
    }
    public static void Shape(JsonNode? value, IEnumerable<string> expected, string reason)
    {
        JsonObject obj = Object(value, reason);
        string[] names = expected.Order(StringComparer.Ordinal).ToArray();
        EfiPeImage.Require(obj.Select(p => p.Key).Order(StringComparer.Ordinal).SequenceEqual(names, StringComparer.Ordinal), reason);
    }
    public static JsonObject Parse(string text, string reason)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new() { MaxDepth = 64, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            ValidateDuplicateProperties(document.RootElement, reason);
            return Object(JsonNode.Parse(text), reason);
        }
        catch (JsonException error) { throw new ToolFailure(reason, "type=" + error.GetType().Name); }
    }
    private static void ValidateDuplicateProperties(JsonElement element, string reason)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) { EfiPeImage.Require(names.Add(property.Name), reason, "duplicate=" + property.Name); ValidateDuplicateProperties(property.Value, reason); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) ValidateDuplicateProperties(child, reason);
    }
    public static XDocument Xml(string text, string reason)
    {
        try
        {
            using var stringReader = new StringReader(text);
            using var reader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumTextBytes });
            return XDocument.Load(reader);
        }
        catch (XmlException error) { throw new ToolFailure(reason, "type=" + error.GetType().Name); }
    }
    public static IReadOnlyList<string> SourceClosurePaths(string root)
    {
        string projectPath = Path.Combine(root, "src/OS/OS.csproj");
        XDocument project;
        try { project = ContractAudit.ReadProject(projectPath); }
        catch (XmlException) { throw new ToolFailure("source_closure_project_malformed", projectPath); }
        var nodes = project.Descendants("Compile").ToArray();
        EfiPeImage.Require(nodes.Length != 0, "source_closure_compile_inputs_missing");
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            string include = (string?)node.Attribute("Include") ?? "";
            EfiPeImage.Require(!string.IsNullOrWhiteSpace(include) && !include.Any(c => "$*?[]".Contains(c)), "source_closure_compile_input_invalid");
            string full = FullPath(Path.Combine(root, "src/OS"), include, "source_closure_compile_input_invalid");
            string relative = RelativePath(root, full, "source_closure_compile_input_outside_repository");
            // A snapshot represents physical inputs once. Exact compile membership, order and
            // duplicates are independently rejected by locked project admission.
            paths.Add(relative);
        }
        foreach (string path in EfiContracts.SourceClosureExplicitPaths) paths.Add(path);
        IReadOnlyList<string> toolingInputs;
        try { toolingInputs = ToolingBuildInputs.SourceInputs(root); }
        catch (InvalidOperationException error) when (error.Message.StartsWith(ToolingBuildInputs.ReparsePointDiagnosticPrefix, StringComparison.Ordinal))
        {
            throw new ToolFailure("source_closure_reparse_point", error.Message[ToolingBuildInputs.ReparsePointDiagnosticPrefix.Length..]);
        }
        foreach (string path in toolingInputs) paths.Add(RelativePath(root, path, "source_closure_tool_input_outside_repository"));
        // OS.Tooling uses SDK default compile items: bind every actual first-party tool input,
        // pruning generated directories before descent. An added source cannot evade provenance.
        string tooling = Path.Combine(root, "tools/OS.Tooling");
        Stack<string> pending = new(); pending.Push(tooling);
        while (pending.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                EfiPeImage.Require((attributes & FileAttributes.ReparsePoint) == 0, "source_closure_reparse_point", path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (Path.GetFileName(path) is not ("bin" or "obj")) pending.Push(path);
                }
                else if (Path.GetExtension(path) is ".cs" or ".csproj") paths.Add(RelativePath(root, path, "source_closure_input_outside_repository"));
            }
        }
        return paths.Order(StringComparer.Ordinal).ToArray();
    }
    public static EfiSnapshot Snapshot(AuthorityFiles locks, string root, IEnumerable<string> relativePaths, string prefix, long maximumBytes = MaximumFileBytes, IReadOnlyDictionary<string, string>? pins = null, bool singleLink = false)
    {
        List<EfiInput> inputs = []; List<AuthorityFile> records = []; HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (string relative in relativePaths)
        {
            EfiPeImage.Require(seen.Add(relative), prefix + "_input_duplicate", relative);
            string path = CanonicalRelative(root, relative, prefix);
            EfiPeImage.Require(File.Exists(path), prefix + "_input_missing", "path=" + relative);
            AuthorityFile file = locks.Open(root, path, prefix + ":" + relative, prefix, maximumBytes, allowEmpty: true, singleLink: singleLink);
            if (pins is not null && pins.TryGetValue(relative, out string? expected)) EfiPeImage.Require(file.Sha256 == expected, prefix + "_input_hash_mismatch", "path=" + relative);
            inputs.Add(new(relative, file.Sha256, file.Length)); records.Add(file); total = checked(total + file.Length);
        }
        if (singleLink) AuthorityFiles.RequireDistinct(records.ToArray());
        return new(inputs, Aggregate(inputs), total);
    }
    public static EfiSnapshot VerifySnapshot(AuthorityFiles locks, string root, IEnumerable<string> paths, JsonNode? evidence, string prefix, string aggregate, long maximumBytes = MaximumFileBytes, IReadOnlyDictionary<string, string>? pins = null, bool singleLink = false)
    {
        string[] expected = paths.ToArray(); JsonArray entries = Array(evidence, prefix + "_input_shape_invalid");
        EfiPeImage.Require(entries.Count == expected.Length, prefix + "_input_count_mismatch", $"expected={expected.Length} actual={entries.Count}");
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            Shape(entries[i], ["path", "sha256"], prefix + "_input_shape_invalid");
            string path = String(entries[i]!["path"], prefix + "_input_shape_invalid");
            string hash = String(entries[i]!["sha256"], prefix + "_input_hash_malformed");
            EfiPeImage.Require(IsHash(hash), prefix + "_input_hash_malformed");
            EfiPeImage.Require(names.Add(path), prefix + "_input_set_mismatch", path);
            EfiPeImage.Require(path == expected[i], prefix + "_input_set_mismatch", $"index={i} expected={expected[i]} actual={path}");
        }
        var snapshot = Snapshot(locks, root, expected, prefix, maximumBytes, pins, singleLink);
        for (int i = 0; i < entries.Count; i++) EfiPeImage.Require(snapshot.Inputs[i].Sha256 == String(entries[i]!["sha256"], prefix + "_input_hash_malformed"), prefix + "_input_hash_mismatch", "path=" + expected[i]);
        EfiPeImage.Require(snapshot.AggregateSha256 == aggregate, prefix + "_aggregate_mismatch");
        return snapshot;
    }
    public static EfiSnapshot PackageTree(AuthorityFiles locks, string root, string prefix = "package_tree")
    {
        EfiPeImage.Require(Directory.Exists(root), prefix + "_root_missing");
        AuthorityFiles.RequirePlainPath(root, root, prefix + "_reparse_point");
        Stack<string> pending = new(); pending.Push(root); Dictionary<string, string> paths = new(StringComparer.Ordinal);
        int entries = 0; long total = 0;
        while (pending.TryPop(out string? directory)) foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            EfiPeImage.Require(++entries <= 1024, prefix + "_entry_count_out_of_range");
            var attributes = File.GetAttributes(path);
            EfiPeImage.Require((attributes & FileAttributes.ReparsePoint) == 0, prefix + "_reparse_point", path);
            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
            if (Path.GetFileName(path) == ".nupkg.metadata") continue;
            EfiPeImage.Require(paths.Count < 512, prefix + "_file_count_out_of_range");
            long size = new FileInfo(path).Length;
            EfiPeImage.Require(size is >= 0 and <= MaximumFileBytes && total <= 134217728 - size, prefix + "_byte_count_out_of_range");
            total += size; EfiPeImage.Require(paths.TryAdd(RelativePath(root, path, prefix + "_path_invalid"), path), prefix + "_duplicate_path");
        }
        List<EfiInput> inputs = [];
        foreach (var pair in paths.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            AuthorityFile record = locks.Open(root, pair.Value, prefix + ":" + pair.Key, prefix, MaximumFileBytes, allowEmpty: true);
            inputs.Add(new(pair.Key, record.Sha256, record.Length));
        }
        return new(inputs, Aggregate(inputs, true), inputs.Sum(i => i.Bytes));
    }
    public static void VerifyPackageInputs(JsonNode? evidence, EfiSnapshot actual)
    {
        var entries = Array(evidence, "package_tree_input_shape_invalid");
        EfiPeImage.Require(entries.Count == actual.FileCount, "package_tree_input_count_mismatch");
        for (int i = 0; i < entries.Count; i++)
        {
            Shape(entries[i], ["path", "bytes", "sha256"], "package_tree_input_shape_invalid");
            EfiInput input = actual.Inputs[i];
            EfiPeImage.Require(String(entries[i]!["path"], "package_tree_input_shape_invalid") == input.Path && String(entries[i]!["sha256"], "package_tree_input_shape_invalid") == input.Sha256 && Integer(entries[i]!["bytes"], "bytes") == input.Bytes, "package_tree_input_mismatch", "index=" + i);
        }
    }
    public static void VerifyPinnedConfig(string text, string source)
    {
        XDocument document = Xml(text, "pinned_restore_config_invalid"); XElement? root = document.Root;
        EfiPeImage.Require(root is not null && root.Name == "configuration" && !root.HasAttributes, "pinned_restore_config_root_invalid");
        EfiPeImage.Require(document.Descendants().All(e => e.Name.NamespaceName.Length == 0), "pinned_restore_config_namespace_invalid");
        XElement[] children = root!.Elements().ToArray();
        EfiPeImage.Require(children.Length == 3, "pinned_restore_config_child_count_invalid");
        string[] names = ["packageSources", "fallbackPackageFolders", "auditSources"];
        for (int i = 0; i < names.Length; i++) EfiPeImage.Require(children[i].Name == names[i] && !children[i].HasAttributes, "pinned_restore_config_child_invalid", "index=" + i);
        var sources = children[0].Elements().ToArray();
        EfiPeImage.Require(sources.Length == 2 && sources[0].Name == "clear" && !sources[0].HasAttributes && sources[1].Name == "add" && sources[1].Attributes().Count() == 2 && (string?)sources[1].Attribute("key") == "CLROS_PINNED" && SamePath((string?)sources[1].Attribute("value") ?? "", source), "pinned_restore_config_package_sources_invalid");
        foreach (var group in children.Skip(1)) EfiPeImage.Require(group.Elements().Count() == 1 && group.Elements().Single().Name == "clear" && !group.Elements().Single().HasAttributes, "pinned_restore_config_clear_group_invalid");
    }
    public static string[] RestorePaths(string configuration) => new[] { "OS.csproj.nuget.dgspec.json", "OS.csproj.nuget.g.props", "OS.csproj.nuget.g.targets", "project.assets.json", "project.nuget.cache" }.Select(n => $"src/OS/obj/{configuration}/net10.0/win-x64/nuget/{n}").Order(StringComparer.Ordinal).ToArray();
}
