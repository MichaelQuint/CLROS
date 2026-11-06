using System.Reflection;

namespace OS.Tooling;

internal static partial class HostProjectOutputs
{
    /// <summary>
    /// Resolves a managed tool in the caller's own build graph, including its
    /// recorded root target for self-spawn. The loaded root image supplies the
    /// comparison hash, avoiding a cyclic embedded self hash. No path fallback.
    /// </summary>
    internal static string ResolveTool(string root, string projectName, Assembly producer)
    {
        var self = ReadSelf(root, producer);
        var outputs = Read(root, producer);
        return ResolveTool(root, projectName, self, outputs);
    }

    internal static string ResolveTool(string root, string projectName, Output self, IReadOnlyList<Output> outputs)
    {
        if (outputs.Any(output => PathComparer.Equals(output.Name, self.Name)
            || PathComparer.Equals(output.Project, self.Project)
            || PathComparer.Equals(output.TargetPath, self.TargetPath)))
            throw new InvalidOperationException("host_project_outputs_self_conflict");

        var selected = PathComparer.Equals(projectName, self.Name) ? self
            : outputs.SingleOrDefault(output => PathComparer.Equals(output.Name, projectName))
                ?? throw new InvalidOperationException("host_project_output_not_in_build " + projectName);
        Verify(root, selected);
        return selected.TargetPath;
    }

    internal static Output ReadSelf(string root, Assembly producer)
    {
        using var stream = producer.GetManifestResourceStream("host-project-self.txt")
            ?? throw new InvalidOperationException("host_project_self_missing rebuild_required");
        using var reader = new StreamReader(stream);
        string loaded = Contained(root, producer.Location);
        Plain(root, loaded);
        return ParseSelf(root, reader, HashFile(loaded));
    }

    internal static Output ParseSelf(string root, TextReader reader, string loadedHash)
    {
        if (reader.ReadLine() != "CLROS_HOST_PROJECT_SELF_1")
            throw new InvalidOperationException("host_project_self_header");
        string[] fields = (reader.ReadLine() ?? string.Empty).Split('|');
        if (fields.Length != 6 || reader.ReadLine() is not null)
            throw new InvalidOperationException("host_project_self_record");

        // Reuse the child-record parser's path, hash and context validation.
        string row = string.Join('|', fields.Take(2).Append(loadedHash).Concat(fields.Skip(2)));
        return Parse(root, new StringReader(Header + "\n" + row)).Single();
    }
}
