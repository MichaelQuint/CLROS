internal static partial class BuildEfiShellPreflightTests
{
    // Contract tokens select the checker's actual input location. Expected
    // mutations and rejection results remain independently declared by each case.
    private static string SemanticContractPath(string root, string projectPath, string family, string anchor,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contracts)
    {
        string[] matches = contracts.Where(pair =>
        {
            string include = pair.Key.Replace('\\', '/');
            string file = include[(include.LastIndexOf('/') + 1)..];
            return (file == family + ".cs" || file.StartsWith(family + ".", StringComparison.Ordinal))
                && pair.Value.Contains(anchor, StringComparer.Ordinal);
        }).Select(pair => pair.Key).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("semantic_contract_source_missing_or_ambiguous family=" + family + " anchor=" + anchor);
        string path = Path.GetFullPath(matches[0].Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(projectPath)!);
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("semantic_contract_source_outside_fixture");
        return path;
    }

    [HostTest("efi semantic contract mutations follow declared source ownership and reject ambiguity")]
    private static void SemanticContractSourceOwnership()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-semantic-owner-" + Guid.NewGuid().ToString("N"));
        string project = Path.Combine(root, "src", "OS", "OS.csproj");
        const string anchor = "first > second";
        var contracts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["../OS.Kernel/Boot/Owner.Renamed.cs"] = [anchor],
            ["../OS.Kernel/Boot/Other.cs"] = [anchor],
            ["../OS.Kernel/Boot/OwnerOther.cs"] = [anchor]
        };
        HostCheck.Equal(Path.Combine(root, "src", "OS.Kernel", "Boot", "Owner.Renamed.cs"),
            SemanticContractPath(root, project, "Owner", anchor, contracts));
        contracts.Remove("../OS.Kernel/Boot/Owner.Renamed.cs");
        contracts.Add("../OS.Kernel/Moved/Owner.NewPart.cs", [anchor]);
        HostCheck.Equal(Path.Combine(root, "src", "OS.Kernel", "Moved", "Owner.NewPart.cs"),
            SemanticContractPath(root, project, "Owner", anchor, contracts));
        contracts.Add("../OS.Kernel/Boot/Owner.Second.cs", [anchor]);
        Reject("semantic_contract_source_missing_or_ambiguous", contracts, anchor);
        contracts.Remove("../OS.Kernel/Boot/Owner.Second.cs");
        Reject("semantic_contract_source_missing_or_ambiguous", contracts, "missing");
        Reject("semantic_contract_source_outside_fixture", new Dictionary<string, IReadOnlyList<string>>
        {
            ["../../../Owner.Escape.cs"] = [anchor]
        }, anchor);

        void Reject(string reason, IReadOnlyDictionary<string, IReadOnlyList<string>> inputs, string token)
        {
            try
            {
                SemanticContractPath(root, project, "Owner", token, inputs);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith(reason, StringComparison.Ordinal))
            {
                return;
            }
            throw new InvalidOperationException("expected semantic contract ownership rejection " + reason);
        }
    }
}
