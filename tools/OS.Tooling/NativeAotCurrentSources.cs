using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

/// <summary>
/// Resolves reviewed source-only splits and explicitly reviewed current consumer
/// revisions. Original receipt identities remain authoritative for historical
/// artifacts; these alternatives neither rebuild them nor establish equivalence
/// of emitted native code.
/// </summary>
internal static partial class NativeAotCurrentSources
{
    internal static IEnumerable<(string Path, string Hash)> Expand(IEnumerable<(string Path, string Hash)> originals)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var original in originals)
        {
            IEnumerable<(string Path, string Hash)> outputs = [original];
            if (ReviewedRevisions.TryGetValue(original.Path, out var revision))
            {
                if (original.Hash != revision.OriginalHash)
                    throw new InvalidDataException("source_revision_original_identity " + original.Path);

                outputs = [(original.Path, revision.CurrentHash)];
            }
            else if (ReviewedFamilies.TryGetValue(original.Path, out var family))
            {
                if (original.Hash != family.OriginalHash)
                    throw new InvalidDataException("source_family_original_identity " + original.Path);

                outputs = FamilyOutputs(original.Path);
            }

            foreach (var current in outputs)
            {
                if (seen.TryGetValue(current.Path, out string? hash))
                {
                    if (hash != current.Hash)
                        throw new InvalidDataException("source_family_conflicting_identity " + current.Path);
                    continue;
                }

                seen.Add(current.Path, current.Hash);
                yield return current;
            }
        }
    }

    private static IEnumerable<(string Path, string Hash)> FamilyOutputs(string original)
    {
        var family = ReviewedFamilies[original];
        // A producer may already have had several partial files before the split.
        // App names such as Program are local to their project and must not merge.
        return original.StartsWith("tools/OS.Tooling/", StringComparison.Ordinal)
            ? ReviewedFamilies.Where(item => item.Key.StartsWith("tools/OS.Tooling/", StringComparison.Ordinal)
                && item.Value.Type == family.Type).SelectMany(item => item.Value.Outputs)
            : family.Outputs;
    }

    internal static IReadOnlyList<(string Path, string Hash)> AppSources(string app,
        IEnumerable<(string Name, string Hash)> originals) =>
        Expand(originals.Select(source => ($"apps/{app}/{source.Name}", source.Hash))).ToArray();

    /// <summary>
    /// Reserves admitted producer declaration names to their reviewed files below
    /// the tooling source directory, regardless of namespace, nesting or arity.
    /// Conditional and generated-looking files participate; only root build-output
    /// directories are excluded. References to the names remain allowed. This is
    /// a conservative source policy, not an evaluated compiler-input inventory.
    /// </summary>
    internal static void RequireToolingOwnership(string root, IEnumerable<(string Path, string Hash)> originals)
    {
        var families = originals.Where(source => source.Path.StartsWith("tools/OS.Tooling/", StringComparison.Ordinal)
                && ReviewedFamilies.ContainsKey(source.Path))
            .ToArray();
        if (families.Length == 0)
            return;

        _ = Expand(families).ToArray();
        var owners = families.GroupBy(source => ReviewedFamilies[source.Path].Type.Split(':')[^1], StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.SelectMany(source => FamilyOutputs(source.Path)).Select(output => output.Path)
                    .ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        root = Path.GetFullPath(root);
        string directory = Path.Combine(root, "tools", "OS.Tooling");
        AuthorityFiles.RequirePlainPath(root, directory, "source_family_path");
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.TryPop(out string? next))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(next))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if (next == directory && (attributes & FileAttributes.Directory) != 0
                    && Path.GetFileName(path) is "bin" or "obj" or "artifacts")
                    continue;

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("source_family_link " + path);

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                    continue;
                }

                if (!Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                string source = File.ReadAllText(path);
                foreach (var options in CSharpSourceProfiles.ForSource(source, relative))
                {
                    var tree = CSharpSyntaxTree.ParseText(source, options, relative);
                    if (tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS1029"))
                        throw new InvalidDataException("source_family_syntax " + relative);

                    foreach (var type in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>().Where(RepositoryCodeStyle.IsType))
                    {
                        if (owners.TryGetValue(RepositoryCodeStyle.TypeName(type), out var admitted) && !admitted.Contains(relative))
                            throw new InvalidDataException("source_family_extra " + relative);
                    }
                }
            }
        }
    }
}
