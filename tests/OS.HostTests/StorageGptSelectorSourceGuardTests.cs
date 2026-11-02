using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Storage;
using OS.Tooling;

internal static class StorageGptSelectorSourceGuardTests
{
    [HostTest("storage GPT selector source guard binds identity and status routing")]
    private static void IdentityAndStatusRouting()
    {
        const string source = """
            using System;
            using OS.Storage;
            using Selection = OS.Storage.GptPartitionSelector;
            namespace SelectorControl;
            internal static class Candidate
            {
                internal static GptEspLocationReadinessResult Select(GptPartitionTableParseResult parsed,
                    ReadOnlySpan<GptPartitionDescriptor> entries)
                {
                    var state = Selection.SelectByPartitionTypeGuid(parsed, entries,
                        GptParser.EfiSystemPartitionTypeGuid, out var found);
                    if (state == GptPartitionSelectorStatus.Success && found.Succeeded)
                        return GptEspLocationReadinessResult.Success(parsed, found.Descriptor, 512, GptPartitionTableSource.Primary);
                    if (state == GptPartitionSelectorStatus.NotFound)
                        return GptEspLocationReadinessResult.FailMissingEsp(parsed, 0, GptPartitionTableSource.Primary);
                    return default;
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References
            .Append(MetadataReference.CreateFromFile(typeof(GptPartitionSelector).Assembly.Location));
        bool Check(string candidate)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog([new CSharpSourceDocument("moved-selector.cs", "synthetic", syntax)], references);
            return StorageGptSelectorSourceGuard.Check(catalog, "SelectorControl.Candidate", "Select");
        }
        HostCheck.True(Check(source), "the shared selector does not depend on local names or import spelling");
        HostCheck.True(!Check(source.Replace("GptParser.EfiSystemPartitionTypeGuid", "Guid.Empty", StringComparison.Ordinal)),
            "the selected partition type must be the canonical EFI type");
        HostCheck.True(!Check(source.Replace("SelectByPartitionTypeGuid", "SelectByUniquePartitionGuid", StringComparison.Ordinal)),
            "partition-type lookup cannot silently become unique-partition lookup");
        HostCheck.True(!Check(source.Replace("if (state == GptPartitionSelectorStatus.Success", "state = default; if (state == GptPartitionSelectorStatus.Success", StringComparison.Ordinal)),
            "status reassignment cannot impersonate the selector result");
        HostCheck.True(!Check(source.Replace(" && found.Succeeded", "", StringComparison.Ordinal)),
            "successful status also requires an initialized successful selector result");
        HostCheck.True(!Check(source.Replace("if (state == GptPartitionSelectorStatus.Success", "found = default; if (state == GptPartitionSelectorStatus.Success", StringComparison.Ordinal)),
            "reassigning the output cannot retain the selector result identity");
        HostCheck.True(!Check(source.Replace("parsed, found.Descriptor,", "parsed, default,", StringComparison.Ordinal)),
            "success returns the descriptor from the shared selector");
        HostCheck.True(!Check(source.Replace("state == GptPartitionSelectorStatus.NotFound", "state == GptPartitionSelectorStatus.Success", StringComparison.Ordinal)),
            "missing ESP routing is tied to the selector's not-found status");
        HostCheck.True(!Check(source.Replace("return GptEspLocationReadinessResult.FailMissingEsp(parsed, 0, GptPartitionTableSource.Primary);",
            "{ if (false) return GptEspLocationReadinessResult.FailMissingEsp(parsed, 0, GptPartitionTableSource.Primary); return default; }", StringComparison.Ordinal)),
            "a dead nested return cannot impersonate missing ESP routing");
    }
}
