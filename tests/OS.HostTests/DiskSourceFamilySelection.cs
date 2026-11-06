using OS.Tooling;

/// <summary>Selects complete reviewed disk source families from one real Compile
/// inventory. Semantic anchors and existing pins remain independent requirements;
/// discovery neither creates pins nor grants source admission.</summary>
internal static class DiskSourceFamilySelection
{
    internal static IReadOnlyDictionary<string, CSharpSourceDocument> Select(CSharpSourceCatalog catalog, string projectDirectory,
        IReadOnlyDictionary<string, string> reasons, IReadOnlyDictionary<string, string> pins,
        IReadOnlyDictionary<string, IReadOnlyList<CSharpSourceDocument>> anchors)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var compiled = catalog.Documents.ToDictionary(document => Path.GetFullPath(document.Path), comparer);
        Dictionary<string, CSharpSourceDocument> selected = new(StringComparer.Ordinal);
        HashSet<string> paths = new(comparer);
        if (anchors.Count == 0)
            throw new InvalidOperationException("disk_source_family_empty");
        foreach (var family in anchors)
        {
            var members = reasons.Where(pair => pair.Value == family.Key).ToArray();
            if (members.Length == 0 || family.Value.Count == 0)
                throw new InvalidOperationException("disk_source_family_missing " + family.Key);
            HashSet<string> familyPaths = new(comparer);
            foreach (var member in members)
            {
                string path = Path.GetFullPath(member.Key.Replace('\\', Path.DirectorySeparatorChar), projectDirectory);
                if (!compiled.TryGetValue(path, out var document))
                    throw new InvalidOperationException("disk_source_family_not_compiled " + member.Key);
                if (!pins.TryGetValue(member.Key, out string? pin) || !string.Equals(pin, ContractAudit.HashSource(document.Text), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("disk_source_family_unpinned " + member.Key);
                if (!paths.Add(path))
                    throw new InvalidOperationException("disk_source_family_duplicate " + member.Key);
                familyPaths.Add(path);
                selected.Add(member.Key, document);
            }
            foreach (var anchor in family.Value)
                if (!compiled.TryGetValue(Path.GetFullPath(anchor.Path), out var actual)
                    || !ReferenceEquals(actual, anchor) || !familyPaths.Contains(Path.GetFullPath(anchor.Path)))
                    throw new InvalidOperationException("disk_source_family_anchor " + family.Key + " " + anchor.Path);
        }
        return selected;
    }
}
