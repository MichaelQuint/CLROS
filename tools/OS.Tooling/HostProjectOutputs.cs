using System.Reflection;
using System.Security.Cryptography;

namespace OS.Tooling;

/// <summary>Reads the reference graph selected during this host producer's build.
/// The embedded resource is bound by its compiler receipt; each target has a
/// separate captured byte hash. Directory snapshots cover sidecars/runtime assets
/// and detect additions as well as edits/removals. Native execution ownership is
/// still governed by its separate qualification contracts.</summary>
internal static partial class HostProjectOutputs
{
    private const string Header = "CLROS_HOST_PROJECT_OUTPUTS_1";

    internal static IReadOnlyList<Output> Read(string root, Assembly producer)
    {
        using var stream = producer.GetManifestResourceStream("host-project-outputs.txt")
            ?? throw new InvalidOperationException("host_project_outputs_missing rebuild_required");
        using var reader = new StreamReader(stream);
        return Parse(root, reader);
    }

    internal static IReadOnlyList<Output> Parse(string root, TextReader reader)
    {
        if (reader.ReadLine() != Header)
            throw new InvalidOperationException("host_project_outputs_header");
        var projects = new Dictionary<string, Output>(PathComparer);
        var names = new HashSet<string>(PathComparer);
        var targets = new HashSet<string>(PathComparer);
        while (reader.ReadLine() is { } line)
        {
            string[] fields = line.Split('|');
            if (fields.Length != 7 || fields[2].Length != 64 || fields[2].Any(value => !Uri.IsHexDigit(value))
                || string.IsNullOrWhiteSpace(fields[3]) || string.IsNullOrWhiteSpace(fields[5])
                || fields.Any(field => field.Contains('\0')))
                throw new InvalidOperationException("host_project_outputs_record");
            string project = Contained(root, fields[0]);
            string target = Contained(root, fields[1]);
            if (!Path.GetExtension(project).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("host_project_outputs_project_kind");
            var output = new Output(project, target, fields[2].ToUpperInvariant(), fields[3], fields[4], fields[5], fields[6]);
            if (projects.TryGetValue(project, out var prior))
            {
                // Diamond references can return the same context repeatedly.
                // A project selected in conflicting contexts is not guessed.
                if (prior != output)
                    throw new InvalidOperationException("host_project_outputs_ambiguous_context " + project);
                continue;
            }
            if (!names.Add(output.Name) || !targets.Add(target))
                throw new InvalidOperationException("host_project_outputs_ambiguous_identity " + project);
            projects.Add(project, output);
        }
        return projects.Values.ToArray();
    }

    internal static string Resolve(string root, string projectName, Assembly producer)
    {
        var output = Read(root, producer).SingleOrDefault(output => PathComparer.Equals(output.Name, projectName))
            ?? throw new InvalidOperationException("host_project_output_not_in_build " + projectName);
        Verify(root, output);
        return output.TargetPath;
    }

    internal static void Verify(string root, Output output)
    {
        Plain(root, output.Project);
        Plain(root, output.TargetPath);
        _ = AdmitHash(output, HashFile(output.TargetPath));
    }

    internal static string AdmitHash(Output output, string capturedHash)
    {
        if (capturedHash != output.Sha256)
            throw new InvalidOperationException("host_project_output_changed rebuild_required " + output.Project);
        return output.Sha256;
    }

    internal static IReadOnlyList<string> Files(string root, Assembly producer) => FilesInDirectories(root,
        Read(root, producer).Select(output => Path.GetDirectoryName(output.TargetPath)!).Append(AppContext.BaseDirectory));

    internal static IReadOnlyDictionary<string, string> Snapshot(string root, IReadOnlyList<Output> outputs,
        IEnumerable<string> additionalDirectories)
    {
        var admitted = outputs.ToDictionary(output => output.TargetPath, PathComparer);
        foreach (var output in outputs)
            Plain(root, output.Project);
        var files = FilesInDirectories(root, outputs.Select(output => Path.GetDirectoryName(output.TargetPath)!)
            .Concat(additionalDirectories));
        var membership = files.ToHashSet(PathComparer);
        if (!admitted.Keys.All(membership.Contains))
            throw new InvalidOperationException("host_project_output_missing rebuild_required");
        var snapshot = new Dictionary<string, string>(PathComparer);
        foreach (string path in files)
        {
            string hash = HashFile(path);
            if (admitted.TryGetValue(path, out var output))
                hash = AdmitHash(output, hash);
            snapshot.Add(path, hash);
        }
        return snapshot;
    }

    internal static IReadOnlyList<string> FilesInDirectories(string root, IEnumerable<string> directories)
    {
        var files = new HashSet<string>(PathComparer);
        foreach (string directory in directories.Distinct(PathComparer))
        {
            string path = Contained(root, directory);
            Plain(root, path);
            Walk(path);
        }
        return files.Order(StringComparer.Ordinal).ToArray();

        void Walk(string directory)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("host_project_output_link " + entry);
                if ((attributes & FileAttributes.Directory) != 0)
                    Walk(entry);
                else
                    files.Add(Path.GetFullPath(entry));
            }
        }
    }

    private static string Contained(string root, string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("host_project_output_absolute_path_required");
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("host_project_output_outside_root");
        return full;
    }

    private static void Plain(string root, string path)
    {
        try
        {
            AuthorityFiles.RequirePlainPath(root, path, "host_project_output_missing_or_link");
        }
        catch (ToolFailure error)
        {
            throw new InvalidOperationException(error.Message, error);
        }
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
}
