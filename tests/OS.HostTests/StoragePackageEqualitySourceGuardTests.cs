using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Storage;
using OS.Tooling;

internal static class StoragePackageEqualitySourceGuardTests
{
    [HostTest("storage package equality guard binds path origin and branch result")]
    private static void PathOriginAndResult()
    {
        const string source = """
            using System;
            using OS.Storage;
            using Bytes = OS.Std.MemoryExtensions;
            namespace PackageEqualControl;
            internal static class Candidate
            {
                internal static OsPackageFileLookupResult Lookup(ReadOnlySpan<byte> package, ReadOnlySpan<byte> requested)
                {
                    OsPackageFileEntry current = default;
                    var candidate = SliceRange(package, current.PathOffset, current.PathLength);
                    if (Bytes.SequenceEqual(candidate, requested)) return OsPackageFileLookupResult.Success(default, current);
                    return default;
                }
                internal static OsPackageStatus Duplicates(ReadOnlySpan<byte> package)
                {
                    OsPackageFileEntry current = default, prior = default;
                    var candidate = SliceRange(package, current.PathOffset, current.PathLength);
                    var previous = SliceRange(package, prior.PathOffset, prior.PathLength);
                    if (Bytes.SequenceEqual(candidate, previous)) return OsPackageStatus.DuplicatePath;
                    return OsPackageStatus.Success;
                }
                private static ReadOnlySpan<byte> SliceRange(ReadOnlySpan<byte> bytes, ulong start, ulong count) =>
                    bytes.Slice((int)start, (int)count);
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References
            .Append(MetadataReference.CreateFromFile(typeof(OsPackageParser).Assembly.Location));
        bool Check(string candidate, bool duplicate = false)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("moved-package-equality.cs", "synthetic", syntax)], references);
            return StoragePackageEqualitySourceGuard.Check(catalog, "PackageEqualControl.Candidate", duplicate ? "Duplicates" : "Lookup", duplicate);
        }
        HostCheck.True(Check(source) && Check(source, true), "bound path origins tolerate aliases and local renames");
        HostCheck.True(!Check(source.Replace("SequenceEqual(candidate, requested)", "SequenceEqual(candidate, package)", StringComparison.Ordinal)),
            "lookup compares the requested path rather than another caller span");
        HostCheck.True(!Check(source.Replace("current.PathLength", "current.ContentLength", StringComparison.Ordinal)),
            "path equality consumes path fields rather than content fields");
        HostCheck.True(!Check(source.Replace("if (Bytes.SequenceEqual(candidate, requested))", "candidate = package; if (Bytes.SequenceEqual(candidate, requested))", StringComparison.Ordinal)),
            "a reassigned path cannot retain its entry origin");
        HostCheck.True(!Check(source.Replace("Success(default, current)", "Success(default, default)", StringComparison.Ordinal)),
            "successful lookup returns the compared entry");
        HostCheck.True(!Check(source.Replace("SequenceEqual(candidate, previous)", "SequenceEqual(candidate, candidate)", StringComparison.Ordinal), true),
            "duplicate detection compares two different entries");
        HostCheck.True(!Check(source.Replace("return OsPackageStatus.DuplicatePath", "return OsPackageStatus.Success", StringComparison.Ordinal), true),
            "duplicate equality directly returns DuplicatePath");
        HostCheck.True(!Check(source.Replace("private static ReadOnlySpan<byte> SliceRange(",
            "private static bool PathsEqual(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) => false; private static ReadOnlySpan<byte> SliceRange(", StringComparison.Ordinal)),
            "the parser does not regain a custom path equality helper");
    }
}
