using System.Security.Cryptography;

namespace OS.Tooling;

internal static partial class NativeAotCurrentSources
{
    // Historical identities select reviewed alternatives; current bytes never
    // become their own expected hash. Keep this inventory derived from the same
    // registrations consumed by package and retained-source admission.
    internal static IReadOnlyList<(string Path, string Hash)> ReviewedSourcesForAudit() =>
        Expand(ReviewedFamilies.Select(family => (family.Key, family.Value.OriginalHash))
            .Concat(ReviewedRevisions.Where(revision => !ReviewedFamilies.ContainsKey(revision.Key))
                .Select(revision => (revision.Key, revision.Value.OriginalHash))))
            .ToArray();

    internal static IReadOnlyList<ContractMismatch> InspectReviewedSources(string root)
    {
        root = Path.GetFullPath(root);
        List<ContractMismatch> failures = [];
        IReadOnlyList<(string Path, string Hash)> sources;
        try
        {
            sources = ReviewedSourcesForAudit();
        }
        catch (InvalidDataException error)
        {
            return [new("nativeaot.reviewed.inventory", "consistent reviewed registrations", error.Message)];
        }

        foreach (var source in sources)
        {
            string actual;
            try
            {
                actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, source.Path))));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                actual = "<unreadable:" + error.GetType().Name + ">";
            }

            if (!source.Hash.Equals(actual, StringComparison.OrdinalIgnoreCase))
                failures.Add(new("nativeaot.reviewed.source:" + source.Path, source.Hash, actual));
        }

        try
        {
            RequireToolingOwnership(root, ReviewedFamilies.Select(family => (family.Key, family.Value.OriginalHash)));
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            failures.Add(new("nativeaot.reviewed.ownership", "registered producer declarations only", error.Message));
        }

        return failures;
    }
}
