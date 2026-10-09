using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class DoneSteps
{
    [GeneratedRegex(@"^step([0-9]+)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex StepName();

    public static string Allocate(string root, bool reserve)
    {
        string directory = Path.Combine(Path.GetFullPath(root), "done");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("done_directory_missing");
        int maximum = Directory.EnumerateFiles(directory, "step*.md")
            .Select(path => StepName().Match(Path.GetFileName(path)))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0).Max();
        int next = checked(maximum + 1);
        while (true)
        {
            string name = "step" + next.ToString("000", CultureInfo.InvariantCulture) + ".md";
            if (!reserve) return "done/" + name;
            string path = Path.Combine(directory, name);
            if (Directory.Exists(path)) throw new IOException("target_exists path=done/" + name);
            try
            {
                // CreateNew makes reservations atomic; never overwrite another agent's evidence.
                using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using StreamWriter writer = new(file, new UTF8Encoding(false));
                writer.Write($"# Step {next} - Reserved\n\n## Summary\n\n## Why\n\n## Files changed\n\n## Tests run\n\n## Evidence\n\n## Failures\n\n## Deferred work\n\n## Evidence boundary\n");
                return "done/" + name;
            }
            catch (IOException) when (File.Exists(path)) { next = checked(next + 1); }
        }
    }
}
