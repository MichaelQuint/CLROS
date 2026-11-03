using OS.Tooling;
using System.Security.Cryptography;
using System.Text;

/// <summary>Matches independently reviewed type families to original UTF-8
/// LF/CRLF bytes. Discovery supplies file locations; it never supplies expected
/// hashes or source admission. Unknown or missing types fail closed.</summary>
internal static class HostSourceOracle
{
    // Use when the original oracle admitted exact raw bytes only. Decoded
    // source equality must not silently admit a different encoding or newline.
    internal static bool MatchesRawFamily(CSharpSourceCatalog source,
        IReadOnlyDictionary<string, IReadOnlyList<string>> expectedTypes)
    {
        if (expectedTypes.Count == 0)
            return false;
        return expectedTypes.All(expected => source.TypeSources(expected.Key)
            .Select(document => document.Sha256.ToLowerInvariant()).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Value.Order(StringComparer.Ordinal)));
    }

    internal static bool MatchesUtf8Family(CSharpSourceCatalog source,
        IReadOnlyDictionary<string, IReadOnlyList<string>> expectedTypes)
    {
        if (expectedTypes.Count == 0)
            return false;
        foreach (var expected in expectedTypes)
        {
            var hashes = new List<string>();
            foreach (var document in source.TypeSources(expected.Key))
            {
                string lf = document.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
                string lfHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lf)));
                string crlfHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    lf.Replace("\n", "\r\n", StringComparison.Ordinal))));
                if (!document.Sha256.Equals(lfHash, StringComparison.OrdinalIgnoreCase)
                    && !document.Sha256.Equals(crlfHash, StringComparison.OrdinalIgnoreCase))
                    return false;
                hashes.Add(lfHash.ToLowerInvariant());
            }
            if (!hashes.Order(StringComparer.Ordinal).SequenceEqual(expected.Value.Order(StringComparer.Ordinal)))
                return false;
        }
        return true;
    }
}
