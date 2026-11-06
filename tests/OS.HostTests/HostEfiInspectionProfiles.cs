using OS.Tooling;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

/// <summary>Closes source-inspection selections over the reviewed project's
/// explicit prerequisites. EfiProfile remains the independent admission policy;
/// resolving locations/configurations never supplies expected test behavior.</summary>
internal static class HostEfiInspectionProfiles
{
    internal static EfiProfile Create(string root, IReadOnlyDictionary<string, bool> selected,
        string bootPolicy = "Diagnostic")
    {
        string text = File.ReadAllText(Path.Combine(root, "src/OS/OS.csproj"));
        if (ContractAudit.HashSource(text) != EfiContracts.ProjectSourceSha256)
            throw new InvalidOperationException("inspection_profile_project_identity");
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 4 * 1024 * 1024
        });
        var project = XDocument.Load(reader);
        if (ContractAudit.InspectProject(project).Count != 0)
            throw new InvalidOperationException("inspection_profile_project_contract");
        var prerequisite = new Regex("^'\\$\\((Enable[A-Za-z0-9_]+Probe)\\)' == 'true' and '\\$\\((Enable[A-Za-z0-9_]+Probe)\\)' != 'true'$",
            RegexOptions.CultureInvariant);
        var edges = project.Descendants("Error").Select(element => prerequisite.Match(element.Attribute("Condition")?.Value ?? ""))
            .Where(match => match.Success).Select(match => (Trigger: match.Groups[1].Value, Required: match.Groups[2].Value)).ToArray();
        var flags = selected.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var edge in edges)
                if (flags.TryGetValue(edge.Trigger, out bool enabled) && enabled)
                {
                    if (flags.TryGetValue(edge.Required, out bool present))
                    {
                        if (!present)
                            throw new InvalidOperationException("inspection_profile_explicit_false_prerequisite " + edge.Required);
                    }
                    else
                    {
                        flags.Add(edge.Required, true);
                        changed = true;
                    }
                }
        }
        while (changed);
        return EfiProfile.Create(bootPolicy, flags);
    }
}
