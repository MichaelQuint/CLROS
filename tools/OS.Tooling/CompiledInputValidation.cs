using System.Security.Cryptography;

namespace OS.Tooling;

/// <summary>Raw-byte freshness checks shared by the host harness and compiled tools.</summary>
public static class CompiledInputValidation
{
    public static IReadOnlyList<string> Validate(TextReader reader, IEnumerable<string>? currentInputs = null)
    {
        List<string> failures = [];
        HashSet<string> recorded = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        int inputs = 0;
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int separator = line.LastIndexOf('|');
            if (separator <= 0) throw new InvalidOperationException("build_input_manifest_invalid");
            string path = line[..separator], expected = line[(separator + 1)..];
            if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) throw new InvalidOperationException("build_input_manifest_invalid_hash");
            recorded.Add(Path.GetFullPath(path)); inputs++;
            if (!File.Exists(path)) { failures.Add("missing_build_input " + path); continue; }
            using FileStream file = File.OpenRead(path);
            string actual = Convert.ToHexString(SHA256.HashData(file));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) failures.Add("stale_build_input " + path);
        }
        if (inputs == 0) failures.Add("empty_build_input_manifest");
        if (currentInputs is not null)
            foreach (string path in currentInputs)
                if (!recorded.Contains(Path.GetFullPath(path))) failures.Add("unrecorded_build_input " + path);
        return failures;
    }
}
