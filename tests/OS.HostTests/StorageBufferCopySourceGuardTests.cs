using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class StorageBufferCopySourceGuardTests
{
    [HostTest("storage shared-copy source guard resolves aliases and rejects dependency or failure drift")]
    private static void BoundCopyControls()
    {
        const string source = """
            using System;
            using Copier = OS.Std.Memory.BufferOperations;
            namespace StorageControl;
            internal enum ResultStatus { Success, DestinationTooSmall, Other }
            internal static class Candidate
            {
                internal static ResultStatus Copy(ReadOnlySpan<byte> input, Span<byte> output)
                {
                    var observation = Copier.MemoryCopy(input, output);
                    if (!observation.Succeeded)
                        return ResultStatus.DestinationTooSmall;
                    return ResultStatus.Success;
                }
            }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        bool Check(string candidate)
        {
            var syntax = CSharpSyntaxTree.ParseText(candidate, new CSharpParseOptions(LanguageVersion.CSharp14));
            var catalog = new CSharpSourceCatalog(
                [new CSharpSourceDocument("storage-copy-control.cs", "synthetic", syntax)], references);
            return StorageBufferCopySourceGuard.Check(catalog, "StorageControl.Candidate", "Copy", "StorageControl.ResultStatus");
        }

        HostCheck.True(Check(source), "alias and independently named local/parameters resolve to shared copy");
        HostCheck.True(!Check(source.Replace("!observation.Succeeded", "observation.Succeeded", StringComparison.Ordinal)),
            "copy failure must drive the rejection branch");
        HostCheck.True(!Check(source.Replace("return ResultStatus.DestinationTooSmall;", "return ResultStatus.Other;", StringComparison.Ordinal)),
            "copy failure preserves the independently expected status");
        HostCheck.True(!Check(source.Replace("Copier.MemoryCopy(input, output)", "Copier.MemoryCopy(input, Span<byte>.Empty)", StringComparison.Ordinal)),
            "copy targets the caller-provided destination");
        HostCheck.True(!Check(source.Replace("return ResultStatus.Success;", "input.CopyTo(output); return ResultStatus.Success;", StringComparison.Ordinal)),
            "direct destination CopyTo remains forbidden");
        HostCheck.True(!Check(source.Replace("using Copier = OS.Std.Memory.BufferOperations;", "using Copier = StorageControl.Impostor;", StringComparison.Ordinal)
            + """

            internal static class Impostor
            {
                internal static OS.Std.Memory.BufferOperationResult MemoryCopy(ReadOnlySpan<byte> input, Span<byte> output) => default;
            }
            """), "same method spelling in an unrelated type is not the shared helper");
    }
}
