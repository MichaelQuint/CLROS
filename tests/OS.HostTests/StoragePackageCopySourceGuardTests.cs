using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Storage;
using OS.Tooling;

internal static class StoragePackageCopySourceGuardTests
{
    [HostTest("storage package copy source guard binds content and caller range across local renames")]
    private static void ContentAndRangeControls()
    {
        const string source = """
            using System;
            using OS.Storage;
            using Copier = OS.Std.Memory.BufferOperations;
            namespace CopyControl;
            internal static class Candidate
            {
                internal static OsPackageReadFileResult Copy(ReadOnlySpan<byte> image, ReadOnlySpan<byte> name,
                    ulong start, ulong length, Span<byte> output)
                {
                    var selected = OsPackageParser.GetFileContent(image, name);
                    ReadOnlySpan<byte> fragment = selected.Content.Slice((int)start, (int)length);
                    var copied = Copier.MemoryCopy(fragment, output);
                    if (!copied.Succeeded)
                        return OsPackageReadFileResult.Fail(OsPackageFileSystemStatus.DestinationTooSmall,
                            OsPackageStatus.Success, default, default, 0, length);
                    return OsPackageReadFileResult.Success(default, default, length);
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References
            .Append(MetadataReference.CreateFromFile(typeof(OsPackageParser).Assembly.Location));
        bool Check(string candidate)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("renamed-copy.cs", "synthetic", syntax)], references);
            return StoragePackageCopySourceGuard.Check(catalog, "CopyControl.Candidate", "Copy", range: true);
        }
        HostCheck.True(Check(source), "bound file content and range do not depend on local names or import spelling");
        HostCheck.True(!Check(source.Replace("GetFileContent(image, name)", "GetFileContent(name, image)", StringComparison.Ordinal)),
            "the file lookup receives the caller's package and path in their API roles");
        HostCheck.True(!Check(source.Replace("Slice((int)start, (int)length)", "Slice(0, (int)length)", StringComparison.Ordinal)),
            "the requested offset reaches the actual copied slice");
        HostCheck.True(!Check(source.Replace("Slice((int)start, (int)length)", "Slice((int)start, 1)", StringComparison.Ordinal)),
            "the requested count reaches the actual copied slice");
        HostCheck.True(!Check(source.Replace("Copier.MemoryCopy(fragment, output)", "Copier.MemoryCopy(image, output)", StringComparison.Ordinal)),
            "an unused correct slice cannot conceal a copy from the whole package");
        HostCheck.True(!Check(source.Replace("var copied =", "fragment = image; var copied =", StringComparison.Ordinal)),
            "reassigning a slice cannot retain its original source identity");
        HostCheck.True(!Check(source.Replace("ReadOnlySpan<byte> fragment =", "selected = default; ReadOnlySpan<byte> fragment =", StringComparison.Ordinal)),
            "reassigning the lookup result cannot retain its original content identity");
    }
}
